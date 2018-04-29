using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using System.IO;

[RequireComponent(typeof(AudioSource))]
public class RecordAndSerializeAudio : MonoBehaviour
{
    // Audio source for playing back audio
    AudioSource _AudioSource;

    // UI
    public Button _PlayButton;
    public Button _RecordButton;
   
    // Audio clip vars
    List<AudioClip> _Clips = new List<AudioClip>();
    AudioClip _RecordingClip;

    public string _NamePrefix = "AudioRecording";   
    public int _MaxClipLength = 10;  // Maximum length of audio recording
    public float _TrimCutoff = .01f;  // Minimum volume of the clip before it gets trimmed    
    int _SampleRate = 44100;    // Audio sample rate
    bool _RecActive = false;    

    public bool _LoadClipsAtStart = true;

    // Use this for initialization
    void Start ()
    {
        _AudioSource = GetComponent<AudioSource>();
        
        _PlayButton.onClick.AddListener(() => PlayRandom());
        _RecordButton.onClick.AddListener(() => RecordToggle());

        if(_LoadClipsAtStart)
            Load();
    }

    void Load()
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
   

    void PlayRandom()
    {
        print("Trying to play random...");

        if (_Clips.Count > 0)
        {
            print("Playing random...");
            _AudioSource.clip = _Clips[Random.Range(0, _Clips.Count)];
            _AudioSource.Play();            
        }
    }

    void RecordToggle()
    {
        if (!_RecActive)
        {
            print("Recording...");
            _RecActive = true;
            _RecordingClip = Microphone.Start("Built-in Microphone", true, _MaxClipLength, _SampleRate);

            _RecordButton.GetComponentInChildren<Text>().text = "Stop recording";
            Invoke("EndRecord", _MaxClipLength);
        }
        else
        {
            print("Recording stopped.");
            _RecActive = false;
            Microphone.End("Built-in Microphone");

            _RecordButton.GetComponentInChildren<Text>().text = "Record";

            CancelInvoke();

            int clipIndex = _Clips.Count;            

            AudioClip trimmedClip = SavWav.TrimSilence(_RecordingClip, _TrimCutoff);

            if(trimmedClip == null)
            {
                print("Clip trimmed to 0");
                return;
            }          

            // Add new clip to the list
            _Clips.Add(trimmedClip);

            // Save recording
            string path = GetRecordingName(clipIndex);           
            SavWav.Save(GetRecordingName(clipIndex), trimmedClip);
            PlayerPrefs.SetInt("storedClipCount", _Clips.Count);
        }
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
