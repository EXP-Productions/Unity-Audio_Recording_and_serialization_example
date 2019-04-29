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
    public Slider _RecordTimerSlider;

    // Audio clip vars
    List<AudioClip> _Clips = new List<AudioClip>();
    // Clip that is being recorded
    AudioClip _RecordingClip;
    // Recorind gdevice index
    public int      _RecordingDeviceIndex = 0;

    // File name prefx
    public string   _NamePrefix = "AudioRecording";
    // Maximum length of audio recording
    public int      _MaxClipDUration = 10;
    // Minimum volume of the clip before it gets trimmed    
    public float    _TrimCutoff = .01f;
    // Audio sample rate
    int             _SampleRate = 44100;     

    public bool     _LoadClipsAtStart = true;

    float _RecordingTimer = 0;

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

        _RecordTimerSlider.maxValue = _MaxClipDUration;

        if (_LoadClipsAtStart)
            LoadAllClips();
    }

    private void Update()
    {
        if(_State == State.Recording)
        {
            _RecordingTimer += Time.deltaTime;
            _RecordTimerSlider.value = _RecordingTimer;

            if(_RecordingTimer >= _MaxClipDUration)            
                EndRecording();
        }
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

            _RecordingClip = Microphone.Start(InputDevice, true, _MaxClipDUration, _SampleRate);
            _RecordButton.GetComponentInChildren<Text>().text = "Stop recording";

            // Update Slider
            _RecordingTimer = 0;
            _RecordTimerSlider.value = _RecordingTimer;
            _RecordTimerSlider.gameObject.SetActive(true);

            _PlayButton.gameObject.SetActive(false);
        }
        else if(_State == State.Recording)
        {          
            EndRecording();
        }
    }

    void EndRecording()
    {
        print("Recording stopped.");
        _State = State.Idle;

        Microphone.End(InputDevice);

        _RecordButton.GetComponentInChildren<Text>().text = "Record";

        int clipIndex = _Clips.Count;

        AudioClip trimmedClip = SavWav.TrimSilence(_RecordingClip, _TrimCutoff);

        if (trimmedClip == null)
        {
            print("Clip trimmed to 0");
            return;
        }

        // Add new clip to the list
        _Clips.Add(trimmedClip);

        // Update Slider
        _RecordingTimer = 0;
        _RecordTimerSlider.value = _RecordingTimer;
        _RecordTimerSlider.gameObject.SetActive(false);

        _PlayButton.gameObject.SetActive(true);

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
