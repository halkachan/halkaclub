using System;
using UnityEngine;

namespace Halka.Game.Player
{
    public sealed class PlayerFootstepAudio : MonoBehaviour
    {
        [SerializeField] private PlayerMover mover;
        [SerializeField] private AudioSource source;
        [SerializeField] private AudioClip footstep;

        private void Awake()
        {
            if (mover == null || source == null || footstep == null)
                throw new InvalidOperationException("Footstep audio references are incomplete");
            footstep.LoadAudioData();
        }

        private void OnEnable()
        {
            if (mover != null) mover.StepStarted += OnStepStarted;
        }

        private void OnDisable()
        {
            if (mover != null) mover.StepStarted -= OnStepStarted;
        }

        private void OnStepStarted(Vector2Int destinationCell)
        {
            source.PlayOneShot(footstep);
        }
    }
}
