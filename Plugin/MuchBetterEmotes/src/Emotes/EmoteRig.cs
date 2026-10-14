using System;
using System.Collections.Generic;
using GameNetcodeStuff;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Experimental.Animations;
using UnityEngine.Playables;
using Random = UnityEngine.Random;

namespace MuchBetterEmotes;

internal sealed class EmoteRig : MonoBehaviour
{
    private const string GraphName = "MuchBetterEmotes";
    private const string IdleState = "Idle1";
    private const float MinPitch = 0.59f;
    private const float MaxPitch = 0.79f;
    private const float NoiseRange = 22f;
    private const float NoiseLoudness = 0.6f;
    private const int NoiseId = 6;
    private const int RemoteNoiseId = 7;

    private readonly AnimationClipPlayable[] _inputs = new AnimationClipPlayable[EmoteDefs.InputCount];
    private readonly float[] _lengths = new float[EmoteDefs.InputCount];

    private PlayableGraph _graph;
    private AnimationMixerPlayable _mixer;
    private AnimationPlayableOutput _output;
    private Animator _animator = null!;
    private PlayerControllerB _player = null!;
    private bool _built;

    public bool Built => _built;
    public Blend Blend { get; private set; } = null!;
    public RigWeights Weights { get; private set; } = null!;
    public float Output { get; set; }
    public FadeMode Mode { get; set; }
    public int Current { get; set; }
    public int Requested { get; set; }
    public float SpeedApplied { get; set; }
    public float SpeedOriginal { get; set; }
    public float SpeedMultiplier { get; set; }
    public GameObject? Sign { get; set; }
    public MeshRenderer? Board { get; set; }
    public MeshRenderer? Back { get; set; }
    public Renderer? Legs { get; set; }
    public GrabbableObject? Held { get; set; }
    public bool HidingItem { get; set; }
    public List<BeltBagItem> Bags { get; } = new();

    public static EmoteRig? Of(PlayerControllerB? player)
    {
        if (player == null || player.playerBodyAnimator == null) return null;
        return player.playerBodyAnimator.GetComponent<EmoteRig>();
    }

    public void Play(int emote)
    {
        if (!_built || !EmoteDefs.TryGet(emote, out EmoteDef def)) return;

        bool fromRest = Current == 0;
        Current = emote;
        if (fromRest && !SyncRest())
        {
            Blend.Snap(def.Input);
        }
        else
        {
            Blend.FadeTo(def.Input, def.Fade);
        }
        StartClip(def);
    }

    public void Stop()
    {
        if (!_built) return;

        Current = 0;
        SyncRest();
        Blend.FadeTo(0, Constants.Rig.StopFade);
    }

    public void Apply(float dt)
    {
        if (!_built) return;

        WrapLoop(dt);
        Blend.Update(dt);
        IReadOnlyList<float> weights = Blend.Weights;
        int target = Blend.Target;
        for (int i = 0; i < weights.Count; i++)
        {
            float weight = weights[i];
            _mixer.SetInputWeight(i, weight);
            if (weight <= 0f && i != target)
            {
                _inputs[i].SetSpeed(0d);
            }
        }
        _output.SetWeight(Output);
    }

    public void PlayClapSound()
    {
        if (Current != EmoteDefs.Clap && Current != EmoteDefs.DoubleClap) return;

        AudioClip[] claps = AssetsLoader.Claps;
        if (!_player.performingEmote || claps.Length == 0) return;

        _player.movementAudio.pitch = Random.Range(MinPitch, MaxPitch);
        _player.movementAudio.PlayOneShot(claps[Random.Range(0, claps.Length)]);

        bool shipClosed = _player.isInHangarShipRoom && StartOfRound.Instance.hangarDoorsClosed;
        int noiseId = _player.IsOwner ? NoiseId : RemoteNoiseId;
        RoundManager.Instance.PlayAudibleNoise(_player.transform.position, NoiseRange, NoiseLoudness, 0, shipClosed, noiseId);
    }

    public void PlayFootstepSound()
    {
        if (!EmoteDefs.HasSteps(Current) || !_player.performingEmote) return;

        if (_player.IsOwner)
        {
            _player.PlayFootstepLocal();
        }
        else
        {
            _player.PlayFootstepServer();
        }
    }

    private void Awake()
    {
        _player = GetComponentInParent<PlayerControllerB>();
        if (_player == null)
        {
            Destroy(this);
            return;
        }

        Blend = new Blend(EmoteDefs.InputCount);
        Weights = new RigWeights(_player);
        Weights.Capture();

        try
        {
            Build();
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"Failed to set up emotes for player {_player.playerClientId}: {ex}");
            if (_graph.IsValid())
            {
                _graph.Destroy();
            }
        }
    }

    private void OnDestroy()
    {
        _built = false;
        if (_graph.IsValid())
        {
            _graph.Destroy();
        }
    }

    private void Build()
    {
        _animator = GetComponent<Animator>();
        AnimationClip[] clips = AssetsLoader.Clips;

        _graph = PlayableGraph.Create(GraphName);
        _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
        _mixer = AnimationMixerPlayable.Create(_graph, EmoteDefs.InputCount);
        for (int i = 0; i < EmoteDefs.InputCount; i++)
        {
            _inputs[i] = AnimationClipPlayable.Create(_graph, clips[i]);
            _lengths[i] = clips[i].length;
            _graph.Connect(_inputs[i], 0, _mixer, i);
            _mixer.SetInputWeight(i, i == 0 ? 1f : 0f);
            _inputs[i].SetSpeed(0d);
        }

        _output = AnimationPlayableOutput.Create(_graph, GraphName, _animator);
        _output.SetAnimationStreamSource(AnimationStreamSource.PreviousInputs);
        _output.SetSortingOrder(Constants.Rig.SortingOrder);
        _output.SetSourcePlayable(_mixer);
        _output.SetWeight(0f);
        _graph.Play();
        _built = true;
    }

    private bool SyncRest()
    {
        AnimatorStateInfo info = _animator.GetCurrentAnimatorStateInfo(0);
        if (!info.IsName(IdleState)) return false;

        SetTimeQuiet(_inputs[0], (info.normalizedTime % 1f) * _lengths[0]);
        _inputs[0].SetSpeed(1d);
        return true;
    }

    private void WrapLoop(float dt)
    {
        if (Current == 0 || !EmoteDefs.TryGet(Current, out EmoteDef def) || def.LoopStart <= 0f) return;

        AnimationClipPlayable input = _inputs[def.Input];
        float time = (float)input.GetTime();
        float speed = (float)input.GetSpeed();
        if (LoopMath.TryWrap(time, dt, speed, _lengths[def.Input], def.LoopStart, out float wrapped))
        {
            SetTimeQuiet(input, wrapped);
        }
    }

    private void StartClip(EmoteDef def)
    {
        AnimationClipPlayable input = _inputs[def.Input];
        SetTimeQuiet(input, 0d);
        input.SetSpeed(def.Speed);
    }

    private static void SetTimeQuiet(AnimationClipPlayable input, double time)
    {
        input.SetTime(time);
        input.SetTime(time);
    }
}
