using UnityEngine;


namespace RaceExperiment
{
    public class TemporalSound : ManagedMonoBehaviour
    {
        private AudioSource _sfxSource;
        private AudioSource _generatedNoiseSource;

        public AudioClip StartSound;
        private float _startVolume = 0.8f;

        public AudioClip GotAnswer;
        private float _gotAnswerVolume = 1f;

        public AudioClip AnswerIsRight;
        private float _answerIsRightVolume = 1f;

        public AudioClip AnswerIsWrong;
        private float _answerIsWrongVolume = 1f;

        public AudioClip AnswerIsLate;
        private float _answerIsLateVolume = 0.5f;

        public AudioClip Noise;
        private float _noiseVolume = 0.18f;

        public override void ManagedAwake()
        {
            _sfxSource = gameObject.AddComponent<AudioSource>();
            _sfxSource.playOnAwake = false;
            _sfxSource.spatialBlend = 0f;    // 2D sound

            _generatedNoiseSource = gameObject.AddComponent<AudioSource>();
            _generatedNoiseSource.clip = Noise;
            _generatedNoiseSource.volume = _noiseVolume;
            _generatedNoiseSource.loop = true;
            _generatedNoiseSource.playOnAwake = false;
            _generatedNoiseSource.spatialBlend = 0f;    // 2D sound
        }


        public void PlaySound_Start() => _sfxSource.PlayOneShot(StartSound, _startVolume);
        public void PlaySound_GotAnswer() => _sfxSource.PlayOneShot(GotAnswer, _gotAnswerVolume);
        public void PlaySound_AnswerIsRight() => _sfxSource.PlayOneShot(AnswerIsRight, _answerIsRightVolume);
        public void PlaySound_AnswerIsWrong() => _sfxSource.PlayOneShot(AnswerIsWrong, _answerIsWrongVolume);
        public void PlaySound_AnswerIsLate() => _sfxSource.PlayOneShot(AnswerIsLate, _answerIsLateVolume);

        public string GetNoiseStatus => _generatedNoiseSource.isPlaying ? "ON" : "OFF";

        public void StartWhiteNoise()
        {
            if (Noise == null) return;
            if (_generatedNoiseSource == null) return;

            if (!_generatedNoiseSource.isPlaying)
                _generatedNoiseSource.Play();
        }

        public void StopWhiteNoise()
        {
            if (_generatedNoiseSource == null) return;

            if (_generatedNoiseSource.isPlaying)
                _generatedNoiseSource.Stop();
        }

        public void ToggleWhiteNoise()
        {
            if (_generatedNoiseSource == null) return;

            if (_generatedNoiseSource.isPlaying)
                StopWhiteNoise();
            else
                StartWhiteNoise();
        }
    }
}