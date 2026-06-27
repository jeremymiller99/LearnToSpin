using UnityEngine;
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

        private void Start()
        {
            // 1. Initialize the VCAs by passing their exact FMOD paths
            _masterVCA = RuntimeManager.GetVCA("vca:/MasterVCA");
            _musicVCA = RuntimeManager.GetVCA("vca:/MusicVCA");
            _sfxVCA = RuntimeManager.GetVCA("vca:/SFXVCA");
        
            // 2. CREATE ALL INSTANCES (If these are missing, the loops will silently fail!)
            _rollGroundInst = RuntimeManager.CreateInstance(rollGroundRef);
            _rollWoodInst = RuntimeManager.CreateInstance(rollWoodRef);
            _airWhooshInst = RuntimeManager.CreateInstance(airWhooshRef);
            _moneyChangeInst = RuntimeManager.CreateInstance(moneyChangeRef);
            _tireRevInst = RuntimeManager.CreateInstance(tireRevRef);
            
            // 3. Start the music
            _musicInst = RuntimeManager.CreateInstance(musicRef);
            _musicInst.start();
            SetMusicMode(1); 
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
            _masterVCA.setVolume(Mathf.Clamp01(volume));
        }

        /// <summary>
        /// Sets the Music volume.
        /// </summary>
        /// <param name="volume">Float between 0.0f (mute) and 1.0f (full volume)</param>
        public void SetMusicVolume(float volume)
        {
            _musicVCA.setVolume(Mathf.Clamp01(volume));
        }

        /// <summary>
        /// Sets the SFX volume.
        /// </summary>
        /// <param name="volume">Float between 0.0f (mute) and 1.0f (full volume)</param>
        public void SetSFXVolume(float volume)
        {
            _sfxVCA.setVolume(Mathf.Clamp01(volume));
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
            _musicInst.setParameterByName("music_mode", mode);
        }
    }
}
