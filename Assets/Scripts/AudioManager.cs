using UnityEngine;
using UnityEngine.InputSystem;
using FMODUnity;
using FMOD.Studio;

namespace LearnToSpin
{
    /// <summary>
    /// Global audio manager for FMOD events. 
    /// Handles one-shots and continuous looping instances (rolling, air, revving, music).
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Tricks & Landing")]
        public EventReference trickNiceRef;
        public EventReference trickSickRef;
        public EventReference trickInsaneRef;
        public EventReference trickGodlikeRef;
        public EventReference landRef;

        [Header("Rolling & Air")]
        public EventReference rollGroundRef;
        public EventReference rollWoodRef;
        public EventReference airWhooshRef;

        [Header("UI & Money")]
        public EventReference btnHoverRef;
        public EventReference btnClickRef;
        public EventReference moneyChangeRef;
        public EventReference moneyStopRef;

        [Header("Launch")]
        public EventReference launchPerfectRef;
        public EventReference tireRevRef;
        public EventReference tireLaunchRef;
        public EventReference tireImpactRef;

        [Header("Music")]
        public EventReference musicRef;

        // ---- Event Instances for Looping Sounds ----
        private EventInstance _rollGroundInst;
        private EventInstance _rollWoodInst;
        private EventInstance _airWhooshInst;
        private EventInstance _moneyChangeInst;
        private EventInstance _tireRevInst;
        private EventInstance _musicInst;

        // ---- Volume Control (VCAs) ----
        private VCA _masterVCA;
        private VCA _musicVCA;
        private VCA _sfxVCA;
        private bool _vcaReady;

        // Persisted volume levels (0..1). Defaults to full until loaded.
        private const string PrefMaster = "vol_master";
        private const string PrefMusic  = "vol_music";
        private const string PrefSFX    = "vol_sfx";
        private float _masterVolume = 1f;
        private float _musicVolume  = 1f;
        private float _sfxVolume    = 1f;

        // ---- Music start gating (WebGL autoplay) ----
        // Browsers refuse to play audio until the first user gesture, so on WebGL we DON'T start the
        // looping music at scene load (it would be kicked into a suspended audio context and stay
        // silent for the whole session). Instead we hold it until the first click/keypress. SFX are
        // unaffected — they only ever fire in response to input, by which point audio is unlocked.
        private bool _musicStarted;
        private bool _awaitingGesture;
        private int  _pendingMusicMode = 1;

        public float MasterVolume => _masterVolume;
        public float MusicVolume  => _musicVolume;
        public float SFXVolume    => _sfxVolume;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }

        private System.Collections.IEnumerator Start()
        {
            // On WebGL the banks load ASYNCHRONOUSLY (web requests), so this Start can run BEFORE the
            // Master bank is in memory. GetVCA / CreateInstance against an unloaded bank silently
            // return invalid handles — and because the looping instances (music, rev, rolling, …) are
            // created here exactly once, they then stay silent forever, while one-shots survive only
            // because PlayOneShot fires later once the bank is up. (As the bank grew with more audio,
            // this load lost its race with Start, which is why it "suddenly" broke with no code change.)
            // Wait for the Master bank — which holds every event and VCA — before touching any of it.
            while (!RuntimeManager.HaveMasterBanksLoaded)
                yield return null;

            // 1. Initialize the VCAs by passing their exact FMOD paths
            _masterVCA = RuntimeManager.GetVCA("vca:/MasterVCA");
            _musicVCA = RuntimeManager.GetVCA("vca:/MusicVCA");
            _sfxVCA = RuntimeManager.GetVCA("vca:/SFXVCA");
            _vcaReady = true;

            // Apply any volume levels saved from a previous session (defaults to full).
            SetMasterVolume(PlayerPrefs.GetFloat(PrefMaster, 1f));
            SetMusicVolume(PlayerPrefs.GetFloat(PrefMusic, 1f));
            SetSFXVolume(PlayerPrefs.GetFloat(PrefSFX, 1f));

            // 2. CREATE ALL INSTANCES (If these are missing, the loops will silently fail!)
            _rollGroundInst = RuntimeManager.CreateInstance(rollGroundRef);
            _rollWoodInst = RuntimeManager.CreateInstance(rollWoodRef);
            _airWhooshInst = RuntimeManager.CreateInstance(airWhooshRef);
            _moneyChangeInst = RuntimeManager.CreateInstance(moneyChangeRef);
            _tireRevInst = RuntimeManager.CreateInstance(tireRevRef);
            
            // 3. Create the music instance. On WebGL, hold the actual start() until the first user
            //    gesture (see _awaitingGesture / Update) so it doesn't begin in a suspended, silent
            //    audio context. Everywhere else (editor, desktop) start it right away as before.
            _musicInst = RuntimeManager.CreateInstance(musicRef);
#if UNITY_WEBGL && !UNITY_EDITOR
            _awaitingGesture = true;
#else
            StartMusic();
#endif
        }

        /// <summary>Actually start the looping music instance and apply the latest requested mode.
        /// Called immediately on desktop/editor, or on the first user input on WebGL.</summary>
        private void StartMusic()
        {
            if (_musicStarted) return;
            _musicInst.start();
            _musicInst.setParameterByName("music_mode", _pendingMusicMode);
            _musicStarted = true;
        }

        private void Update()
        {
            // WebGL: start the held music on the first click/tap/key. Also nudge the mixer to resume
            // in case the browser's audio context is still suspended at that instant (harmless if it's
            // already running). No-op on every other platform.
            if (!_awaitingGesture) return;
            if (FirstUserGesture())
            {
                _awaitingGesture = false;
                RuntimeManager.CoreSystem.mixerResume();
                StartMusic();
            }
        }

        private static bool FirstUserGesture()
        {
            var m = Mouse.current;
            if (m != null && m.leftButton.wasPressedThisFrame) return true;
            var k = Keyboard.current;
            if (k != null && k.anyKey.wasPressedThisFrame) return true;
            var t = Touchscreen.current;
            if (t != null && t.primaryTouch.press.wasPressedThisFrame) return true;
            return false;
        }

        // ====================================================================================
        // SETTINGS MENU HOOKS
        // ====================================================================================

        /// <summary>
        /// Sets the Master volume.
        /// </summary>
        /// <param name="volume">Float between 0.0f (mute) and 1.0f (full volume)</param>
        public void SetMasterVolume(float volume)
        {
            _masterVolume = Mathf.Clamp01(volume);
            if (_vcaReady) _masterVCA.setVolume(_masterVolume);
            PlayerPrefs.SetFloat(PrefMaster, _masterVolume);
        }

        /// <summary>
        /// Sets the Music volume.
        /// </summary>
        /// <param name="volume">Float between 0.0f (mute) and 1.0f (full volume)</param>
        public void SetMusicVolume(float volume)
        {
            _musicVolume = Mathf.Clamp01(volume);
            if (_vcaReady) _musicVCA.setVolume(_musicVolume);
            PlayerPrefs.SetFloat(PrefMusic, _musicVolume);
        }

        /// <summary>
        /// Sets the SFX volume.
        /// </summary>
        /// <param name="volume">Float between 0.0f (mute) and 1.0f (full volume)</param>
        public void SetSFXVolume(float volume)
        {
            _sfxVolume = Mathf.Clamp01(volume);
            if (_vcaReady) _sfxVCA.setVolume(_sfxVolume);
            PlayerPrefs.SetFloat(PrefSFX, _sfxVolume);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                // Clean up instances to prevent memory leaks
                ReleaseInstance(ref _rollGroundInst);
                ReleaseInstance(ref _rollWoodInst);
                ReleaseInstance(ref _airWhooshInst);
                ReleaseInstance(ref _moneyChangeInst);
                ReleaseInstance(ref _tireRevInst);
                ReleaseInstance(ref _musicInst);
            }
        }

        private void ReleaseInstance(ref EventInstance inst)
        {
            inst.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
            inst.release();
        }

        // ====================================================================================
        // ONE-SHOTS
        // ====================================================================================

        public void PlayTrickNice() => RuntimeManager.PlayOneShot(trickNiceRef);
        public void PlayTrickSick() => RuntimeManager.PlayOneShot(trickSickRef);
        public void PlayTrickInsane() => RuntimeManager.PlayOneShot(trickInsaneRef);
        public void PlayTrickGodlike() => RuntimeManager.PlayOneShot(trickGodlikeRef);
        
        public void PlayLand(Vector3 position = default) => RuntimeManager.PlayOneShot(landRef, position);
        public void PlayLaunchPerfect() => RuntimeManager.PlayOneShot(launchPerfectRef);
        public void PlayTireLaunch() => RuntimeManager.PlayOneShot(tireLaunchRef);
        public void PlayTireImpact(Vector3 position = default) => RuntimeManager.PlayOneShot(tireImpactRef, position);
        
        public void PlayBtnHover() => RuntimeManager.PlayOneShot(btnHoverRef);
        public void PlayBtnClick() => RuntimeManager.PlayOneShot(btnClickRef);

        // ====================================================================================
        // LOOPING & PARAMETER CONTROL
        // ====================================================================================

        // C# State tracking to prevent FMOD async thread spam
        private bool _isRevving;
        private bool _isRollingGround;
        private bool _isRollingWood;
        private bool _isAirWhoosh;
        private bool _isMoneyCounting;

        public void SetRevving(bool isRevving)
        {
            if (_isRevving == isRevving) return; // Do nothing if we are already in this state
            _isRevving = isRevving;

            if (isRevving) _tireRevInst.start();
            else _tireRevInst.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
        }

        public void SetRolling(bool isRollingGround, bool isRollingWood)
        {
            if (_isRollingGround != isRollingGround)
            {
                _isRollingGround = isRollingGround;
                if (isRollingGround) _rollGroundInst.start();
                else _rollGroundInst.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            }

            if (_isRollingWood != isRollingWood)
            {
                _isRollingWood = isRollingWood;
                if (isRollingWood) _rollWoodInst.start();
                else _rollWoodInst.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            }
        }

        public void SetAirWhoosh(bool isAirborne, float velocityNormalized)
        {
            if (_isAirWhoosh != isAirborne)
            {
                _isAirWhoosh = isAirborne;
                if (isAirborne) _airWhooshInst.start();
                else _airWhooshInst.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            }

            if (isAirborne)
            {
                _airWhooshInst.setParameterByName("velocity", Mathf.Clamp01(velocityNormalized));
            }
        }

        public void SetMoneyCounting(bool isCounting)
        {
            if (_isMoneyCounting == isCounting) return;
            _isMoneyCounting = isCounting;

            if (isCounting) _moneyChangeInst.start();
            else _moneyChangeInst.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
        }

        public void PlayMoneyStop() 
        {
            RuntimeManager.PlayOneShot(moneyStopRef);
        }

        public void SetMusicMode(int mode)
        {
            // Remember the request so the right mode is applied if the music hasn't started yet
            // (WebGL holds the start until the first gesture); apply live once it's running.
            _pendingMusicMode = mode;
            if (_musicStarted) _musicInst.setParameterByName("music_mode", mode);
        }
    }
}
