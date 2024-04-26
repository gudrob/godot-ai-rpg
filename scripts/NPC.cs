using Godot;
using System;

public partial class NPC : CharacterBody3D
{
    string characterName = "Firstname Lastname";
    readonly Random randomGenerator = new();

    private float speechIntensity = 0.1f;
    private float speechDuration;
    private float speechLipsThinnerBase;
    private float speechLipsSmallerBase;
    private float speechLipsMoveForewardBase;
    private float speechCheecksThinnerBase;
    private float[] speechSpectrumValues = new float[10];
    private int speechSpectrumValuesCount = 0;
    private bool speechIsPlaying = false;
    private int speechAudioBus;

    private int jawIndex = 0;
    private float jawBaseAngle = 0;


    private bool autoBlink = false;
    private bool autoBlinkClosing = false;
    private float autoBlinkSpeed = 5f;
    private float autoBlinkClosed = 0f;
    private float autoBlinkBlinkTime = 0f;
    private float autoBlinkInterval = 0f;
    private float autoBlinkDeviation = 0f;

    private int rightUpperLidIndex = 0;
    private int leftUpperLidIndex = 0;

    private int rightLowerLidIndex = 0;
    private int leftLowerLidIndex = 0;

    private Action speechCallback;

    AudioStreamPlayer3D audioPlayer;

    AnimationPlayer animationPlayer;

    Skeleton3D skeleton;

    [Export]
    public Node3D lookAtTarget;

    SkeletonIK3D headIK;

    public override void _Ready()
    {
        AudioBusManager.Setup(2, 3);

        skeleton = FindChild("skeleton") as Skeleton3D;

        headIK = FindChild("head_IK") as SkeletonIK3D;

        skeleton.ResetBonePoses();

        jawIndex = skeleton.FindBone("Jaw");
        jawBaseAngle = skeleton.GetBonePoseRotation(jawIndex).Y;

        leftUpperLidIndex = skeleton.FindBone("uplid.L");
        rightUpperLidIndex = skeleton.FindBone("uplid.R");

        rightLowerLidIndex = skeleton.FindBone("lolid.R");
        leftLowerLidIndex = skeleton.FindBone("lolid.L");

        audioPlayer = FindChild("audio_player") as AudioStreamPlayer3D;

        animationPlayer = FindChild("animation_player") as AnimationPlayer;

        EnableAutoBlink(3.33f, 0.66f);

        headIK.Target = lookAtTarget.Transform;
    }

    public override void _Process(double delta)
    {
        ProcessSpeech((float)delta);
    }

    private void ProcessAutoBlinking(float delta)
    {
        var deltaSpeed = delta * autoBlinkSpeed;

        if (!autoBlink)
        {
            if (autoBlinkClosed > 0)
            {
                autoBlinkClosed -= deltaSpeed;

                if (autoBlinkClosed < 0) deltaSpeed += autoBlinkClosed;

                AdjustEyeLids(-deltaSpeed);
            }

            return;
        }

        if (autoBlinkClosing)
        {
            autoBlinkClosed += deltaSpeed;

            if (autoBlinkClosed >= 0.3f)
            {
                autoBlinkClosed = 0.3f;
                autoBlinkClosing = false;
            }
        }
        else
        {
            if (autoBlinkClosed <= 0)
            {
                autoBlinkBlinkTime -= delta;

                if (autoBlinkBlinkTime <= 0)
                {
                    autoBlinkClosing = true;
                }

                return;
            }

            autoBlinkClosed -= deltaSpeed;

            if (autoBlinkClosed < 0)
            {
                autoBlinkClosed = 0;
                autoBlinkBlinkTime = autoBlinkInterval + autoBlinkDeviation * (randomGenerator.NextSingle() - 0.5f);
            }
        }

        AdjustEyeLids(autoBlinkClosed);
    }

    /// <summary>
    /// Play audio from a <paramref name="stream"/> on a high or low priority bus specified by <paramref name="highPriority"/>. At the end of playing the audio, <paramref name="callback"/> will be invoked. Set <paramref name="speechIntensity"/> to adjust how much the jaw moves. Returns true if there was a free bus and the sound is playing.
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="highPriority"></param>
    /// <param name="callback"></param>
    /// <param name="speechIntensity"></param>
    /// <returns></returns>
    public bool Speak(AudioStream stream, bool highPriority, Action callback = null, float speechIntensity = 0.1f)
    {
        var duration = (float)stream.GetLength();

        speechSpectrumValues = new float[10];

        speechAudioBus = AudioBusManager.GetBus(duration, highPriority);

        if (speechAudioBus == -1) return false;

        speechIsPlaying = true;
        this.speechIntensity = speechIntensity;
        speechDuration = duration;
        audioPlayer.Bus = AudioServer.GetBusName(speechAudioBus);
        audioPlayer.Stream = stream;
        audioPlayer.Play();

        speechCallback?.Invoke();
        speechCallback = callback;

        return true;
    }

    public bool IsSpeaking()
    {
        return audioPlayer.Playing;
    }

    private void ProcessSpeech(float delta)
    {
        if (speechDuration <= 0) return;

        speechDuration -= delta;

        var jawRotation = skeleton.GetBonePoseRotation(jawIndex);

        if (speechDuration <= 0)
        {
            jawRotation.Y = jawBaseAngle;
            speechCallback?.Invoke();
            speechIsPlaying = false;
        }
        else
        {
            speechSpectrumValuesCount = (speechSpectrumValuesCount + 1) % 10;

            var spectrumValue = AudioBusManager.GetSpectrum(speechAudioBus);
            speechSpectrumValues[speechSpectrumValuesCount] = spectrumValue;
            spectrumValue =
                speechSpectrumValues[(speechSpectrumValuesCount + 1) % 10] * 0.03f +
                speechSpectrumValues[(speechSpectrumValuesCount + 2) % 10] * 0.05f +
                speechSpectrumValues[(speechSpectrumValuesCount + 3) % 10] * 0.07f +
                speechSpectrumValues[(speechSpectrumValuesCount + 4) % 10] * 0.09f +
                speechSpectrumValues[(speechSpectrumValuesCount + 5) % 10] * 0.11f +
                speechSpectrumValues[(speechSpectrumValuesCount + 6) % 10] * 0.13f +
                speechSpectrumValues[(speechSpectrumValuesCount + 7) % 10] * 0.15f +
                speechSpectrumValues[(speechSpectrumValuesCount + 8) % 10] * 0.17f +
                speechSpectrumValues[(speechSpectrumValuesCount + 9) % 10] * 0.19f +
                speechSpectrumValues[speechSpectrumValuesCount] * 0.23f;

            jawRotation.Y = jawBaseAngle - spectrumValue * 0.3f;
        }

        skeleton.SetBonePoseRotation(jawIndex, jawRotation);

    }

    /// <summary>
    /// Adjusts the eye lid bones based on the value of <paramref name="closed"/>
    /// </summary>
    /// <param name="closed"></param>
    private void AdjustEyeLids(float closed)
    {
        var positionLeftUpper = skeleton.GetBonePosePosition(leftUpperLidIndex);
        var positionRightUpper = skeleton.GetBonePosePosition(rightUpperLidIndex);

        var positionLeftLower = skeleton.GetBonePosePosition(leftLowerLidIndex);
        var positionRightLower = skeleton.GetBonePosePosition(rightLowerLidIndex);

        var value = 0.057f - 0.025f * closed;
        var value2 = 0.057f + 0.005f * closed;

        positionLeftUpper.Y = value;
        positionRightUpper.Y = value;

        positionLeftLower.Y = value2;
        positionRightLower.Y = value2;

        skeleton.SetBonePosePosition(leftUpperLidIndex, positionLeftUpper);
        skeleton.SetBonePosePosition(rightUpperLidIndex, positionRightUpper);

        skeleton.SetBonePosePosition(leftLowerLidIndex, positionLeftLower);
        skeleton.SetBonePosePosition(rightLowerLidIndex, positionRightLower);
    }

    /// <summary>
    /// Enables auto blinking in intervals of <paramref name="interval"/> seconds with a deviation of <paramref name="deviation"/> seconds and a given <paramref name="speed"/>
    /// </summary>
    /// <param name="interval"></param>
    /// <param name="deviation"></param>
    /// <param name="speed"></param>
    public void EnableAutoBlink(float interval, float deviation, float speed = 5.0f)
    {
        autoBlinkDeviation = deviation;
        autoBlinkInterval = interval;
        autoBlinkSpeed = speed;
        autoBlink = true;
    }

    /// <summary>
    /// Disables auto blinking
    /// </summary>
    public void DisableAutoBlink()
    {
        autoBlink = false;
    }

}
