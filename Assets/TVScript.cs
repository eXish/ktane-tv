﻿using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using KeepCoding;
using UnityEngine;
using UnityEngine.Video;

public class TVScript : MonoBehaviour
{
    public GameObject RigFab;
    public AnimationCurve WobbleCurve;
    public VideoPlayer Player;
    public AudioScript Source1, Source2, SourceStatic;
    public MeshRenderer staticMat;
    public KMSelectable buttonVol, buttonPow, buttonBrg, buttonCon, buttonChn;
    public GameObject staticScreen, videoScreen, blackScreen, artifactScreen;
    public KMBombInfo Info;
    public KMBombModule Module;

    public Texture2D[] _artifacts;
    public TextMesh _text;

    private int _id;
    private static int _idC;

    private int dirMode, spinMode;
    private int screenMode = 3;

    private int volume = 1;
    private bool _firstTwitchCommand;

    private static VideoClip[] clips = new VideoClip[0];
    private int currentClip = 0;

    private bool _solved = false;

    private Material _artifact;
    private CameraRig Rig;
    private readonly List<int> _artifactsUsed = new List<int>();
    private int _artifactIx;
    private string _currentText;
    private float _textOff;

    void Start()
    {
        GameObject c = Instantiate(RigFab);
        c.transform.position = transform.position * 10 + new Vector3(0f, 1000f, 0f);
        Rig = c.GetComponent<CameraRig>();

        _id = ++_idC;
        dirMode = Random.Range(0, 4);
        Debug.LogFormat("[TV #{0}] Pointing {1}.", _id, new string[] { "up", "right", "down", "left" }[dirMode % 4]);
        StartCoroutine(Spin(dirMode));

        spinMode = Random.Range(0, 2);
        Debug.LogFormat("[TV #{0}] Spinning {1}.", _id, new string[] { "clockwise", "counter-clockwise" }[spinMode]);
        StartCoroutine(SpinSpinner());

#if UNITY_EDITOR
        if (clips.Length == 0)
        {
            clips = GetComponent<EditorVideos>().clips;
        }
#else
        if(clips.Length == 0)
        {
            clips = KeepCoding.PathManager.GetAssets<VideoClip>("tvvideo");
        }
#endif
        Rig.Camera.targetTexture = new RenderTexture(Rig.Camera.targetTexture);
        staticMat.material = new Material(staticMat.material)
        {
            mainTexture = Rig.Camera.targetTexture
        };
        Player.clip = clips[1];
        Player.SetTargetAudioSource(0, Source1.AudioSource);
        Player.SetTargetAudioSource(1, Source2.AudioSource);
        Source1.Volume = volume / 10f;
        Source2.Volume = volume / 10f;
        SourceStatic.Volume = volume / 10f;
        Player.Play();
        SourceStatic.AudioSource.Play();

        Player.isLooping = true;
        Player.loopPointReached += NextVideo;

        buttonVol.OnInteract += () => { buttonVol.AddInteractionPunch(0.1f); volume += 1; volume %= 11; Source1.Volume = volume / 10f; Source2.Volume = volume / 10f; SourceStatic.Volume = volume / 10f; AdjustText(); return false; };
        buttonPow.OnInteract += () => { buttonPow.AddInteractionPunch(0.1f); Power(); return false; };
        buttonChn.OnInteract += () => { buttonChn.AddInteractionPunch(0.1f); Channel(); return false; };
        Power();
        Channel();

        buttonBrg.OnInteract += () => { buttonBrg.AddInteractionPunch(0.1f); LeftRight(); return false; };
        buttonCon.OnInteract += () => { buttonCon.AddInteractionPunch(0.1f); UpDown(); return false; };

        _artifact = artifactScreen.GetComponent<Renderer>().material;
        _artifact.SetColor("_Color", Color.clear);

        _artifactsUsed.Add(Random.Range(0, 8));
        int target = dirMode << 1 | spinMode;
        int artCount = Random.Range(70, 90);
        for (int i = 0; i < artCount; i++)
        {
            int a;
            do
                a = Random.Range(0, 8);
            while (a == _artifactsUsed.Last());
            _artifactsUsed.Add(a);
        }
        if (_artifactsUsed.Last() == target)
        {
            int a;
            do
                a = Random.Range(0, 8);
            while (a == _artifactsUsed.Last());
            _artifactsUsed.Add(a);
        }
        _artifactsUsed.Add(target);
        _artifactsUsed.Add(target);

        _artifactIx = Random.Range(0, _artifactsUsed.Count);

        StartCoroutine(Artifact());
    }

    private void AdjustText()
    {
        _currentText = "VOL~" + (volume == 10 ? "" : "~") + (volume == 0 ? "" : volume.ToString()) + "0";
        _textOff = Time.time + 1f;
        if (screenMode > 1)
            _text.text = _currentText;
    }

    private void Update()
    {
        if (Time.time >= _textOff)
            _text.text = _currentText = "";
    }

    private IEnumerator Artifact()
    {
        while (true)
        {
            byte t = (byte)(Mathf.Max(volume - 3, 0) / 7f * 255f);
            var mult = (20 - volume) / 10f;
            var invmult = 1 / mult;
            int ld = _artifactsUsed[_artifactIx] & 1;
            byte s = ld == 1 ? (byte)0 : (byte)255;
            _artifact.SetColor("_Color", new Color32(s, s, s, t));
            _artifact.SetTexture("_MainTex", _artifacts[_artifactsUsed[_artifactIx] >> 1]);
            yield return new WaitForSeconds(Random.Range(invmult * 0.2f, invmult * 0.4f));
            _artifact.SetColor("_Color", Color.clear);
            yield return new WaitForSeconds(Random.Range(mult * 0.4f, mult * 1.2f));
            _artifactIx++;
            _artifactIx %= _artifactsUsed.Count;
        }
    }

    private void LeftRight()
    {
        if (Mathf.FloorToInt(Info.GetTime()) % 2 == 0)
            Check(3);
        else
            Check(1);
    }

    private void UpDown()
    {
        if (Mathf.FloorToInt(Info.GetTime()) % 2 == 0)
            Check(0);
        else
            Check(2);
    }

    private void Check(int dir)
    {
        if (_solved)
            return;
        if ((dirMode % 4 == dir && spinMode == 0) || ((dirMode + 2) % 4 == dir && spinMode == 1))
        {
            Debug.LogFormat("[TV #{0}] Correct! Solved.", _id);
            Module.HandlePass();
            _solved = true;
            if (TwitchPlaysActive && screenMode > 1)
                Power();
        }
        else
        {
            Debug.LogFormat("[TV #{0}] Struck! You entered {1}, but I expected {2}.", _id, new string[] { "up", "right", "down", "left" }[dir], new string[] { "up", "right", "down", "left" }[(dirMode + (spinMode == 1 ? 2 : 0)) % 4]);
            Module.HandleStrike();
        }
    }

    private void NextVideo(VideoPlayer source)
    {
        currentClip++;
        Player.clip = clips[currentClip %= clips.Length];
        Player.time = 0;
        Player.Play();
    }

    private void Channel()
    {
        if (screenMode % 2 == 0)
        {
            screenMode++;
            buttonChn.transform.localPosition = new Vector3(-0.01f, 0f, 0.0081f);
        }
        else
        {
            screenMode--;
            buttonChn.transform.localPosition = new Vector3(0.01f, 0f, 0.0081f);
        }
        if (screenMode > 1)
            Debug.LogFormat("[TV #{0}] Turned screen to {1}.", _id, new string[] { "Off", "Off", "Channel 3", "Channel 4" }[screenMode]);
        UpdateScreen();
    }

    private void Power()
    {
        screenMode += 2;
        screenMode %= 4;
        Debug.LogFormat("[TV #{0}] Turned screen to {1}.", _id, new string[] { "Off", "Off", "Channel 3", "Channel 4" }[screenMode]);
        UpdateScreen();
    }

    private void UpdateScreen()
    {
        switch (screenMode)
        {
            case 2:
                Source1.AudioSource.mute = true;
                Source2.AudioSource.mute = true;
                SourceStatic.AudioSource.mute = false;
                staticScreen.transform.localPosition = new Vector3(0f, 0f, 0f);
                videoScreen.transform.localPosition = new Vector3(0f, 0f, -0.1f);
                blackScreen.transform.localPosition = new Vector3(0f, 0f, -0.1f);
                artifactScreen.transform.localPosition = new Vector3(0f, 0f, 0.001f);
                Player.Stop();
                _text.text = _currentText;
                break;
            case 3:
                Source1.AudioSource.mute = false;
                Source2.AudioSource.mute = false;
                SourceStatic.AudioSource.mute = true;
                staticScreen.transform.localPosition = new Vector3(0f, 0f, -0.1f);
                videoScreen.transform.localPosition = new Vector3(0f, 0f, 0f);
                blackScreen.transform.localPosition = new Vector3(0f, 0f, -0.1f);
                artifactScreen.transform.localPosition = new Vector3(0f, 0f, -0.1f);
                Player.Play();
                _text.text = _currentText;
                break;
            default:
                Source1.AudioSource.mute = true;
                Source2.AudioSource.mute = true;
                SourceStatic.AudioSource.mute = true;
                staticScreen.transform.localPosition = new Vector3(0f, 0f, -0.1f);
                videoScreen.transform.localPosition = new Vector3(0f, 0f, -0.1f);
                blackScreen.transform.localPosition = new Vector3(0f, 0f, 0f);
                artifactScreen.transform.localPosition = new Vector3(0f, 0f, -0.1f);
                Player.Stop();
                _text.text = "";
                break;
        }
    }

    private IEnumerator Spin(int dirMode)
    {
        bool wobble = Random.Range(0, 2) == 0;
        if (wobble)
            Rig.ArrowParent.localEulerAngles = new Vector3(0f, 90f * dirMode, 0f);
        float timer = 0f;
        while (true)
        {
            timer += Time.deltaTime;
            Rig.Arrow.localEulerAngles = !wobble ? new Vector3(0f, 90f * dirMode + 30f * WobbleCurve.Evaluate(timer / 6f), 0f) : new Vector3(-60f * timer, 0f, 0f);
            yield return null;
        }
    }

    private IEnumerator SpinSpinner()
    {
        while (true)
        {
            Rig.BackSpinner.localEulerAngles += new Vector3(0f, spinMode == 0 ? Time.deltaTime * 60f : Time.deltaTime * -60f, 0f);
            yield return null;
        }
    }

#pragma warning disable 414
    private string TwitchHelpMessage = "Use '!{0} power' to turn the TV on or off. Use '!{0} brightness even' to press brightness on an even digit. Use '!{0} volume 10' to set the volume to 10%. If the gods have given you special powers use '!{0} channel' to press the channel switch.";
#pragma warning restore 414

    private bool TwitchPlaysActive;

    IEnumerator ProcessTwitchCommand(string command)
    {
        if (!_firstTwitchCommand)
        {
            _firstTwitchCommand = true;
            volume = 0;
            Source1.Volume = volume / 10f;
            Source2.Volume = volume / 10f;
            SourceStatic.Volume = volume / 10f;
        }
        command = command.Trim().ToLowerInvariant();
        if (command == "mute")
            command = "volume 0";
        Match m;

        if (Regex.IsMatch(command, @"^(?:press\s+)?power$"))
        {
            yield return null;
            buttonPow.OnInteract();
            yield break;
        }
        if ((m = Regex.Match(command, @"^(?:press\s+)?(brightness|contrast)\s+(even|odd)$")).Success)
        {
            yield return null;
            KMSelectable button = null;
            if (m.Groups[1].Value == "brightness")
                button = buttonBrg;
            if (m.Groups[1].Value == "contrast")
                button = buttonCon;
            if (m.Groups[2].Value == "even")
                yield return new WaitUntil(() => Mathf.FloorToInt(Info.GetTime()) % 2 == 0);
            if (m.Groups[2].Value == "odd")
                yield return new WaitUntil(() => Mathf.FloorToInt(Info.GetTime()) % 2 == 1);
            button.OnInteract();
        }
        if ((m = Regex.Match(command, @"^(?:press\s+|set\s+)?volume(?:\s+(100|00?|\d0)%?)?$")).Success)
        {
            int p = 1;
            if (m.Groups[1].Success && int.TryParse(m.Groups[1].Value, out p))
            {
                p /= 10;
                if (p == volume)
                    yield break;
                if (p > volume)
                    p -= volume;
                else
                    p += 11 - volume;
            }
            yield return null;
            for (int i = 0; i < p; i++)
            {
                buttonVol.OnInteract();
                yield return new WaitForSeconds(0.1f);
            }
            yield break;
        }
        if (Regex.IsMatch(command, @"^(?:press\s+)?channel$"))
        {
            yield return null;
            yield return "antitroll Channel is inaccessible due to concerns over copyright.";
            buttonChn.OnInteract();
            yield break;
        }
    }

    IEnumerator TwitchHandleForcedSolve()
    {
        int solDir = spinMode == 1 ? (dirMode + 2) % 4 : dirMode % 4;
        if (solDir == 0 || solDir == 3)
            while (Mathf.FloorToInt(Info.GetTime()) % 2 != 0) yield return true;
        else
            while (Mathf.FloorToInt(Info.GetTime()) % 2 != 1) yield return true;
        if (solDir == 1 || solDir == 3)
            buttonBrg.OnInteract();
        else
            buttonCon.OnInteract();
    }
}