using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controls the reactive illumination of the green SM_FuseCell on the workbench in Area 7 (Dark Fuse Room).
/// Per design requirements:
/// - In the pitch dark room, the fuse cell is completely unlit (zero glow, black emission).
/// - When Player 4 shines the tactical torch light on the fuse cell, it absorbs the beam and radiantly glows.
/// - When the torch beam points away, the glow smoothly dims back to pitch black.
/// - The player cannot spot or collect the fuse cell in the dark unless the torch beam is shining on it.
/// </summary>
[ExecuteAlways]
[AddComponentMenu("FLIP//FALL/Gameplay/Fuse Cell Illumination Receiver")]
[DisallowMultipleComponent]
public class FuseCellIlluminationReceiver : MonoBehaviour
{
    [Header("Glow Configuration")]
    [SerializeField] private float _maxLightIntensity = 3.6f;
    [SerializeField] private float _maxLightRange = 6.5f;
    [SerializeField] private float _activationSpeed = 4.0f;
    [SerializeField] private float _dimSpeed = 2.5f;

    [Header("Dark Fuse Room Boundary (Area 7)")]
    [Tooltip("World-space minimum boundary of the dark fuse room (Area 7). Inside this volume, glow is suppressed unless torch shines on it.")]
    [SerializeField] private Vector3 _darkRoomMin = new Vector3(24.0f, -1.0f, 1.0f);
    [Tooltip("World-space maximum boundary of the dark fuse room (Area 7).")]
    [SerializeField] private Vector3 _darkRoomMax = new Vector3(48.0f, 6.0f, 21.0f);

    [Header("Detection")]
    [SerializeField] private Vector3 _centerOffset = new Vector3(0f, 0.15f, 0f);
    [SerializeField] private float _detectionRadius = 0.5f;

    [Header("State (Read Only)")]
    [SerializeField] private float _currentGlowFactor = 0f;
    [SerializeField] private bool _isTorchIlluminating = false;

    private Light _attachedGlowLight;
    private MaterialPropertyBlock _propBlock;

    private struct ReactiveRenderer
    {
        public Renderer Renderer;
        public int MaterialIndex;
        public Color TargetEmission;
    }

    private readonly List<ReactiveRenderer> _reactiveRenderers = new List<ReactiveRenderer>(16);

    public bool IsIlluminated => _currentGlowFactor > 0.12f;
    public float CurrentGlowFactor => _currentGlowFactor;

    public bool IsInsideDarkFuseRoom()
    {
        Vector3 pos = transform.position;
        return pos.x >= _darkRoomMin.x && pos.x <= _darkRoomMax.x &&
               pos.y >= _darkRoomMin.y && pos.y <= _darkRoomMax.y &&
               pos.z >= _darkRoomMin.z && pos.z <= _darkRoomMax.z;
    }

    private void Awake()
    {
        InitializeComponents();
    }

    private void OnEnable()
    {
        InitializeComponents();
    }

    private void InitializeComponents()
    {
        if (_propBlock == null) _propBlock = new MaterialPropertyBlock();

        if (_attachedGlowLight == null)
        {
            _attachedGlowLight = GetComponentInChildren<Light>(true);
        }

        if (_reactiveRenderers.Count == 0)
        {
            FindReactiveRenderers();
        }

        ApplyGlow(_currentGlowFactor);
    }

    private void FindReactiveRenderers()
    {
        _reactiveRenderers.Clear();
        Renderer[] rends = GetComponentsInChildren<Renderer>(true);

        foreach (var rend in rends)
        {
            if (rend == null) continue;
            Material[] mats = rend.sharedMaterials;

            for (int i = 0; i < mats.Length; i++)
            {
                Material m = mats[i];
                if (m == null) continue;

                Color targetEmission = Color.black;
                bool isReactive = false;

                string matName = m.name;
                if (matName.Contains("EnergyCore"))
                {
                    targetEmission = new Color(2.16f, 18.0f, 1.44f, 1.0f);
                    isReactive = true;
                }
                else if (matName.Contains("Indicator"))
                {
                    targetEmission = new Color(1.40f, 14.0f, 1.12f, 1.0f);
                    isReactive = true;
                }
                else if (matName.Contains("Bubble"))
                {
                    targetEmission = new Color(0.80f, 4.0f, 1.80f, 1.0f);
                    isReactive = true;
                }
                else if (matName.Contains("Liquid"))
                {
                    targetEmission = new Color(0.044f, 0.468f, 0.121f, 1.0f) * 4.0f;
                    isReactive = true;
                }
                else if (matName.Contains("Glass"))
                {
                    targetEmission = new Color(0.035f, 0.333f, 0.042f, 1.0f);
                    isReactive = true;
                }

                if (isReactive)
                {
                    _reactiveRenderers.Add(new ReactiveRenderer
                    {
                        Renderer = rend,
                        MaterialIndex = i,
                        TargetEmission = targetEmission
                    });
                }
            }
        }
    }

    private void Update()
    {
        // If collected into player inventory, stop world illumination
        if (FuseCellPickup.PlayerHasFuseCell)
        {
            if (_currentGlowFactor > 0f) ApplyGlow(0f);
            return;
        }

        if (_propBlock == null || _reactiveRenderers.Count == 0 || _attachedGlowLight == null)
        {
            InitializeComponents();
        }

        bool insideDarkRoom = IsInsideDarkFuseRoom();

        if (!insideDarkRoom)
        {
            // Outside the dark room, the fuse cell emits its normal full radiant green glow and point light
            _isTorchIlluminating = false;
            float dt = Application.isPlaying ? Time.deltaTime : 0.05f;
            _currentGlowFactor = Mathf.MoveTowards(_currentGlowFactor, 1.0f, dt * _activationSpeed);
            ApplyGlow(_currentGlowFactor);
            return;
        }

        // Inside the dark room: glow is suppressed and restricted strictly to when torch light shines on it
        Vector3 detectPos = transform.position + _centerOffset;
        _isTorchIlluminating = PlayerTorchAbility.IsTorchShiningOn(detectPos, _detectionRadius, out float illuminationIntensity);

        float targetFactor = _isTorchIlluminating ? illuminationIntensity : 0f;
        float speed = _isTorchIlluminating ? _activationSpeed : _dimSpeed;

        float dtDark = Application.isPlaying ? Time.deltaTime : 0.05f;
        _currentGlowFactor = Mathf.MoveTowards(_currentGlowFactor, targetFactor, dtDark * speed);

        ApplyGlow(_currentGlowFactor);
    }

    public void ApplyGlow(float factor)
    {
        _currentGlowFactor = factor;

        if (_propBlock == null) _propBlock = new MaterialPropertyBlock();

        // 1. Attached emerald glow point light
        if (_attachedGlowLight != null)
        {
            _attachedGlowLight.enabled = (factor > 0.01f);
            _attachedGlowLight.intensity = Mathf.Lerp(0f, _maxLightIntensity, factor);
            _attachedGlowLight.range = Mathf.Lerp(0f, _maxLightRange, factor);
        }

        // 2. Child renderer material emissions via MaterialPropertyBlock (no asset mutation)
        for (int i = 0; i < _reactiveRenderers.Count; i++)
        {
            var r = _reactiveRenderers[i];
            if (r.Renderer == null) continue;

            r.Renderer.GetPropertyBlock(_propBlock, r.MaterialIndex);
            Color currentEmission = Color.Lerp(Color.black, r.TargetEmission, factor);
            _propBlock.SetColor("_EmissionColor", currentEmission);
            r.Renderer.SetPropertyBlock(_propBlock, r.MaterialIndex);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.1f, 0.8f, 0.3f, 0.2f);
        Vector3 center = (_darkRoomMin + _darkRoomMax) * 0.5f;
        Vector3 size = _darkRoomMax - _darkRoomMin;
        Gizmos.DrawCube(center, size);
        Gizmos.color = new Color(0.1f, 0.8f, 0.3f, 0.8f);
        Gizmos.DrawWireCube(center, size);
    }
}
