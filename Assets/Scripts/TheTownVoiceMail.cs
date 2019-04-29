using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json;


class ClipPromptPair
{
    public string _ClipPath;
    public string _Prompt;

    public ClipPromptPair(string clipPath, string prompt)
    {
        _ClipPath = clipPath;
        _Prompt = prompt;
    }
}

[RequireComponent(typeof(RecordAndSerializeAudio))]
public class TheTownVoiceMail : MonoBehaviour
{
    RecordAndSerializeAudio RecordAndSerializeAudio;

    public string[] _PromptQuestions;
    public Text _PromptText;
    public Button _RandomizePromptButton;

    List<ClipPromptPair> _ClipPromptPairs = new List<ClipPromptPair>();
    
    string Path { get { return Application.dataPath + "TheTownVoiceMail.json"; } }

    int _CurrentPromptIndex = 0;

    // Start is called before the first frame update
    void Start()
    {
        RecordAndSerializeAudio = GetComponent<RecordAndSerializeAudio>();
        RecordAndSerializeAudio.onSaveEvent += OnSaveEventHandler;

        if (System.IO.File.Exists(Path))
        {
            _ClipPromptPairs = JsonSerialisationHelper.LoadFromFile<List<ClipPromptPair>>(Path) as List<ClipPromptPair>;
            print("Loaded clips prompt pairs: " + _ClipPromptPairs.Count);
        }

        _RandomizePromptButton.onClick.AddListener(() => RandomizePrompt());
    }

    private void OnSaveEventHandler(string filePath)
    {
        _ClipPromptPairs.Add(new ClipPromptPair(filePath, _PromptQuestions[_CurrentPromptIndex]));
    }

    void RandomizePrompt()
    {       
        _CurrentPromptIndex = (int)(Random.value * _PromptQuestions.Length);
        _PromptText.text = _PromptQuestions[_CurrentPromptIndex];
        print("Randomizing prompt: " + _PromptQuestions[_CurrentPromptIndex]);
    }

    private void OnApplicationQuit()
    {
        JsonSerialisationHelper.Save(Path, _ClipPromptPairs);
    }
}
