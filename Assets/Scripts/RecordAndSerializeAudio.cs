using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using System.IO;
using Newtonsoft.Json;


class ClipPromptPair
{
    public string _ClipPath;
    public string _Prompt;

    [JsonIgnore]
    public AudioClip _Clip;

    [JsonConstructor]
    public ClipPromptPair(string clipPath, string prompt)
    {
        Debug.Log("New clip prompt pair: " + prompt + "   " + clipPath);
        _ClipPath = clipPath;
        _Prompt = prompt;
    }

    public ClipPromptPair(string clipPath, string prompt, AudioClip clip)
    {
        Debug.Log("New clip prompt pair: " + prompt + "   " + clipPath);
        _ClipPath = clipPath;
        _Prompt = prompt;
        _Clip = clip;
    }
}

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
    AudioSource _AudioSource;
    float _Timer = 0;
    
    #region UI
    [Header("UI")]
    public Button _PlayButton;
    public Button _RecordButton;
    public Image _PlaybackRadial;
    public Image _RecordRadial;

    public Text _PromptText;
    public Button _RandomizePromptButton;
    #endregion
    
    #region AUDIO VARS
    // Audio clip vars
    [Header("Audio")]
    // Recorind gdevice index
    public int _RecordingDeviceIndex = 0;
    //List<AudioClip> _Clips = new List<AudioClip>();
    // Clip that is being recorded
    AudioClip       _RecordingClip;
    string InputDevice { get { return Microphone.devices[0]; } }
    // Maximum length of audio recording
    public int      _MaxClipDuration = 10;
    // Minimum volume of the clip before it gets trimmed    
    public float    _TrimCutoff = .01f;
    // Audio sample rate
    int             _SampleRate = 44100;
    int             _SelectedClipPromptPairIndex = 0;
    #endregion

    #region SERIALIZATION
    public string   _NamePrefix = "AudioRecording";
    public int      _DataBaseVersionNumber = 0;
    public bool     _LoadClipsAtStart = true;
    List<ClipPromptPair> _ClipPromptPairs = new List<ClipPromptPair>();
    string JSONDBInfoPath { get { return Application.dataPath + "DBInfo.json"; } }
    string JSONClipDBPath { get { return Application.dataPath + _DataBaseVersionNumber + "TheTownVoiceMail.json"; } }
    #endregion

    #region PROMPTS
    [Header("Prompts")]
    public string[] _PromptQuestions;
    int _CurrentPromptIndex = 0;
    #endregion
   
    // Use this for initialization
    void Start ()
    {
        PrintAllRecordingDevices();

         // Get Audio source component
         _AudioSource = GetComponent<AudioSource>();
        
        // Hook up UI
        _PlayButton.onClick.AddListener(() => SetState(State.Playing));
        _RecordButton.onClick.AddListener(() => RecordToggle());
        _RandomizePromptButton.onClick.AddListener(() => RandomizePrompt());
        _PlaybackRadial.fillAmount = 0;
        _RecordRadial.fillAmount = 0;

        if (_LoadClipsAtStart)
        {
            print("Loading all clips...");
            if (System.IO.File.Exists(JSONDBInfoPath))
                _DataBaseVersionNumber = PlayerPrefs.GetInt("_DataBaseVersionNumber");

            print("DB ID: " + _DataBaseVersionNumber);

            if (System.IO.File.Exists(JSONClipDBPath))
            {
                _ClipPromptPairs =  JsonSerialisationHelper.LoadFromFile<List<ClipPromptPair>>(JSONClipDBPath) as List<ClipPromptPair>;
                print("Loaded clips prompt pairs: " + _ClipPromptPairs.Count);
            }
            else
            {
                print("Can't find JSON clip pair file");
            }

            for (int i = 0; i < _ClipPromptPairs.Count; i++)
            {
                var filepath = Path.Combine(Application.persistentDataPath, _ClipPromptPairs[i]._ClipPath);
                filepath = "file:///" + filepath;

                // Starts coroutine that adds clip to the list if completeed successfully
                StartCoroutine(GetAudioClip(_ClipPromptPairs[i]));
            }
        }
    }
    
    private void Update()
    {
        if(_State == State.Recording)
        {
            _Timer += Time.deltaTime;
            _RecordRadial.fillAmount = _Timer / _MaxClipDuration;

            if (_Timer >= _MaxClipDuration)
                SetState(State.Idle);
        }
        else if(_State == State.Playing)
        {
            _Timer += Time.deltaTime;
            _PlaybackRadial.fillAmount = _Timer / _AudioSource.clip.length;

            if (_Timer>= _AudioSource.clip.length)
                SetState(State.Idle);
        }
    }

    void SetState(State state)
    {
        if(state == State.Idle)
        {
            if (_State == State.Recording)
                EndRecording();

            _PlayButton.interactable = true;
            _PlaybackRadial.fillAmount = 0;

            _RecordButton.interactable = true;
            _RecordButton.GetComponentInChildren<Text>().text = "rec msg";
            _RecordRadial.fillAmount = 0;
        }
        else if(state == State.Recording)
        {
            _PlayButton.interactable = false;
            _RecordButton.GetComponentInChildren<Text>().text = "End rec";
            _Timer = 0;

            _RecordingClip = Microphone.Start(InputDevice, true, _MaxClipDuration, _SampleRate);           
        }
        else if (state == State.Playing)
        {
            if (_ClipPromptPairs.Count == 0)
                return;

            _Timer = 0;
            _SelectedClipPromptPairIndex = Random.Range(0, _ClipPromptPairs.Count);
            print("Playing clip par at index: " + _SelectedClipPromptPairIndex);

            _AudioSource.clip = _ClipPromptPairs[_SelectedClipPromptPairIndex]._Clip;
            _AudioSource.Play();

            // UI
            _RecordButton.interactable = false;
            _CurrentPromptIndex = _SelectedClipPromptPairIndex;
            _PromptText.text = _ClipPromptPairs[_SelectedClipPromptPairIndex]._Prompt;
        }

        _State = state;
        print("State: " + _State.ToString());       
    }    

    // Record button function, starts recoridng if in idle state or stops recording if in recording state
    void RecordToggle()
    {
        if (_State == State.Idle)        
            SetState(State.Recording);
        else if(_State == State.Recording)
            SetState(State.Idle);
    }

    void EndRecording()
    {
        print("Recording stopped.");
       
        Microphone.End(InputDevice);

        int clipIndex = _ClipPromptPairs.Count;
        AudioClip trimmedClip = SavWav.TrimSilence(_RecordingClip, _TrimCutoff);

        if (trimmedClip == null)
        {
            print("Clip trimmed to 0");
            return;
        }

        // Add new clip to the list
        //_Clips.Add(trimmedClip);

        // Save recording
        string audioFilePath = GetRecordingName(clipIndex);
        SavWav.Save(audioFilePath, trimmedClip);
        _ClipPromptPairs.Add(new ClipPromptPair(audioFilePath, _PromptText.text, trimmedClip));
    }
    
    void RandomizePrompt()
    {
        _CurrentPromptIndex = (int)(Random.value * _PromptQuestions.Length);
        _PromptText.text = _PromptQuestions[_CurrentPromptIndex];
        print("Randomizing prompt: " + _PromptQuestions[_CurrentPromptIndex]);
    }

    public void NewDB()
    {
        _ClipPromptPairs.Clear();
        //_Clips.Clear();

        _DataBaseVersionNumber++;
        PlayerPrefs.SetInt("_DataBaseVersionNumber", _DataBaseVersionNumber);
        JsonSerialisationHelper.Save(JSONClipDBPath, _ClipPromptPairs);
    }

    private void OnApplicationQuit()
    {
        PlayerPrefs.SetInt("_DataBaseVersionNumber", _DataBaseVersionNumber);
        JsonSerialisationHelper.Save(JSONClipDBPath, _ClipPromptPairs);
    }

    string GetRecordingName(int index)
    {
        return _NamePrefix +"_Recording_" + index.ToString() + ".wav";
    }

    [ContextMenu("Print Recording Devices")]
    void PrintAllRecordingDevices()
    {
        for (int i = 0; i < Microphone.devices.Length; i++)
            print("Recorindg devices: " + Microphone.devices[i] + " " + i);
    }

    IEnumerator GetAudioClip(ClipPromptPair clipPromptPair)
    {
        print("Loading... " + clipPromptPair._ClipPath);

        string filepath = "file:///" + Path.Combine(Application.persistentDataPath, clipPromptPair._ClipPath);       

        AudioClip audioClip = null;
        using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(filepath, AudioType.WAV))        
        {
            yield return www.SendWebRequest();

            if (www.isHttpError || www.isNetworkError)
            {
                Debug.Log(www.error);
            }
            else
            {
                audioClip = DownloadHandlerAudioClip.GetContent(www);
                clipPromptPair._Clip = audioClip;
                //_Clips.Add(audioClip);
                print("Loading complete: " + filepath);
            }
        }
    }
}
