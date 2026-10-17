using System;
using UnityEngine;

namespace Halka.Game.Player
{
    public sealed class PlayerFootstepAudio : MonoBehaviour
    {
        [SerializeField] private PlayerMover mover;
        [SerializeField] private AudioSource source;
        [SerializeField] private AudioClip footstep;
        // ver3.0 shared world: steps come from SharedWorldSession.StepStarted through PlayStep,
        // so no PlayerMover is wired (the 2D game keeps mover and leaves this off).
        [SerializeField] private bool externallyDriven;

        public int PlayCount { get; private set; }

        private void Awake()
        {
            if (mover == null && !externallyDriven || source == null || footstep == null)
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

        private void OnStepStarted(Vector2Int destinationCell) => PlayStep();

        // One footstep for one step that has started (a blocked step never starts).
        public void PlayStep()
        {
            source.PlayOneShot(footstep);
            PlayCount++;
        }
    }
}
