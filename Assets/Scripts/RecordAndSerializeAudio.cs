using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using System.IO;

[RequireComponent(typeof(AudioSource))]
public class RecordAndSerializeAudio : MonoBehaviour
{
    public delegate void OnSave(string filePath);
    public event OnSave onSaveEvent;   


    enum State
    {
        Idle,
        Recording,
        Playing,
    }
        
    State _State = State.Idle;

    // Audio source for playing back audio
    AudioSource _AudioSource;

    // UI
    public Button _PlayButton;
    public Button _RecordButton;
   
    // Audio clip vars
    List<AudioClip> _Clips = new List<AudioClip>();
    // Clip that is being recorded
    AudioClip _RecordingClip;
    // Recorind gdevice index
    public int      _RecordingDeviceIndex = 0;

    // File name prefx
    public string   _NamePrefix = "AudioRecording";
    // Maximum length of audio recording
    public int      _MaxClipLength = 10;
    // Minimum volume of the clip before it gets trimmed    
    public float    _TrimCutoff = .01f;
    // Audio sample rate
    int             _SampleRate = 44100;     

    public bool     _LoadClipsAtStart = true;

    string InputDevice { get { return Microphone.devices[0]; } }

    // Use this for initialization
    void Start ()
    {
        PrintAllRecordingDevices();

         // Get Audio source component
         _AudioSource = GetComponent<AudioSource>();
        
        // Hook up UI
        _PlayButton.onClick.AddListener(() => PlayRandom());
        _RecordButton.onClick.AddListener(() => RecordToggle());

        if(_LoadClipsAtStart)
            LoadAllClips();
    }

    [ContextMenu("Print Recording Devices")]
    void PrintAllRecordingDevices()
    {
        for (int i = 0; i < Microphone.devices.Length; i++)
            print("Recorindg devices: " + Microphone.devices[i] + " " + i);
    }

    // Loads all the clips
    void LoadAllClips()
    {
        int storedClipCount = PlayerPrefs.GetInt("storedClipCount", 0);

        print("Trying to load clips: " + storedClipCount);

        for (int i = 0; i < storedClipCount; i++)
        {
            string path = GetRecordingName(i);
            var filepath = Path.Combine(Application.persistentDataPath, path);
            filepath = "file:///" + filepath;           

            // Starts coroutine that adds clip to the list if completeed successfully
            StartCoroutine(GetAudioClip(filepath));
        }
    }

    // Plays a random clip
    void PlayRandom()
    {
        if(_State == State.Recording)
        {
            print("Can't play a clip while recording");
            return;
        }

        print("Trying to play random...");

        if (_Clips.Count > 0)
        {
            print("Playing random...");
            _AudioSource.clip = _Clips[Random.Range(0, _Clips.Count)];
            _AudioSource.Play();            
        }
    }

    // Record button function, starts recoridng if in idle state or stops recording if in recording state
    void RecordToggle()
    {
        if (_State == State.Idle)
        {
            print("Recording...");
            _State = State.Recording;

            _RecordingClip = Microphone.Start(InputDevice, true, _MaxClipLength, _SampleRate);
            _RecordButton.GetComponentInChildren<Text>().text = "Stop recording";
            Invoke("EndRecord", _MaxClipLength);
        }
        else if(_State == State.Recording)
        {
            CancelInvoke();
            EndRecording();
        }
    }

    void EndRecording()
    {
        print("Recording stopped.");
        _State = State.Idle;

        Microphone.End(InputDevice);

        _RecordButton.GetComponentInChildren<Text>().text = "Record";

        CancelInvoke();

        int clipIndex = _Clips.Count;

        AudioClip trimmedClip = SavWav.TrimSilence(_RecordingClip, _TrimCutoff);

        if (trimmedClip == null)
        {
            print("Clip trimmed to 0");
            return;
        }

        // Add new clip to the list
        _Clips.Add(trimmedClip);

        // Save recording
        string path = GetRecordingName(clipIndex);
        SavWav.Save(path, trimmedClip);
        PlayerPrefs.SetInt("storedClipCount", _Clips.Count);

        if (onSaveEvent != null)
            onSaveEvent(path);
    }

    string GetRecordingName(int index)
    {
        return _NamePrefix +"_Recording_" + index.ToString() + ".wav";
    }

    IEnumerator GetAudioClip(string path)
    {
        print("Loading... " + path);

        AudioClip audioClip = null;
        using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(path, AudioType.WAV))        
        {
            yield return www.SendWebRequest();

            if (www.isHttpError || www.isNetworkError)
            {
                Debug.Log(www.error);
            }
            else
            {
                audioClip = DownloadHandlerAudioClip.GetContent(www);
                _Clips.Add(audioClip);

                print("Loading complete: " + path);
            }
        }
    }
}
