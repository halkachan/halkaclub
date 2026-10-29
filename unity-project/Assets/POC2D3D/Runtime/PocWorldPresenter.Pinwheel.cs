using System.Collections.Generic;
using Halka.Poc2D3D.World;
using UnityEngine;

namespace Halka.Poc2D3D
{
    // ver3.8: the South hill's red pinwheel (「みなみのあかねのかぜのおか」).
    // - かざぐるまをまわす: when she examines the pinwheel (its Action Point), its blades turn: up to speed
    //   at once (SpinUpSeconds), then slower and slower until they stand still again (PinwheelSeconds,
    //   about two turns in all). Only the blades turn (the model's child pinwheel_blades, about its own
    //   South-facing axis through the pin), never the pole. The 2D view draws the same blades from above
    //   (point-sampled, so the pixels stay sharp). One spin at a time: examining it again while it turns
    //   starts nothing new (the message is the Action Point's, shown by the session as before). Nothing
    //   waits: she can walk on at once. No wind, no weather: the blades turn only when she turns them.
    // - The blades stay out of their chunk's static batch (a batched mesh cannot turn); they are part of
    //   the chunk otherwise (built, outlined and destroyed with it, their outline copy in its meshes).
    //   Where they stopped is kept per pinwheel (its Instance ID), so a chunk built again shows them as
    //   they were left.
    public sealed partial class PocWorldPresenter
    {
        public const string PinwheelDefinitionId = "south_pinwheel_red";
        public const string PinwheelBladesName = "pinwheel_blades";
        public const float PinwheelSeconds = 2.2f;
        public const float PinwheelSpinUpSeconds = 0.15f;
        public const float PinwheelTopSpeed = 900f;   // degrees per second, at the top of the spin
        public const float PinwheelPinHeight = 1.2f;  // the blades' pin above the ground (build_v38_south.py PIN_HEIGHT)

        private sealed class PinwheelBlades
        {
            public string InstanceId;
            public Transform Blades;
            public Quaternion Rest;    // world rotation as built (angle 0)
            public Vector3 Axis;       // world axis the blades turn about (seen from the South: clockwise)
        }

        private sealed class PinwheelSpin
        {
            public float StartedAt;
            public float StartAngle;
            public bool Active;
            public float Angle;
        }

        private readonly List<PinwheelBlades> pinwheelBlades = new List<PinwheelBlades>();
        private readonly Dictionary<string, PinwheelSpin> pinwheelSpins = new Dictionary<string, PinwheelSpin>();

        public int PinwheelSpinsStarted { get; private set; }
        public int PinwheelRetriggersIgnored { get; private set; }
        public int PinwheelBladesLoaded => pinwheelBlades.Count;
        public bool PinwheelSpinning
        {
            get
            {
                foreach (var spin in pinwheelSpins.Values)
                    if (spin.Active) return true;
                return false;
            }
        }

        // The angle (degrees, growing) of a pinwheel's blades; 0 before its first spin.
        public float PinwheelAngle(string instanceId) => pinwheelSpins.TryGetValue(instanceId, out var spin) ? spin.Angle : 0f;

        // The blades' Transform of a built pinwheel (null: not drawn now). Checks only.
        public Transform PinwheelBladesOf(string instanceId)
        {
            foreach (var item in pinwheelBlades)
                if (item.InstanceId == instanceId) return item.Blades;
            return null;
        }

        // How far the blades have turned t seconds into a spin: speed up over SpinUpSeconds, then the
        // speed falls off (quadratically) to nothing at PinwheelSeconds.
        public static float PinwheelTurn(float t)
        {
            const float a = PinwheelSpinUpSeconds;
            const float w = PinwheelTopSpeed;
            if (t <= 0f) return 0f;
            if (t <= a) return w * t * t / (2f * a);
            var rest = PinwheelSeconds - a;
            var u = Mathf.Clamp01((t - a) / rest);
            var falling = 1f - u;
            return w * a * 0.5f + w * rest * (1f - falling * falling * falling) / 3f;
        }

        public static float PinwheelFullTurn => PinwheelTurn(PinwheelSeconds);

        // A chunk's pinwheels: their blades leave the static batch and are registered for turning.
        // Returns the GameObjects static batching may combine (null: everything, there is no pinwheel).
        private GameObject[] RegisterChunkPinwheels(ChunkPayload payload)
        {
            List<Transform> blades = null;
            foreach (var view in payload.Views)
            {
                if (view == null || view.DefinitionId != PinwheelDefinitionId) continue;
                var child = FindChild(view.transform, PinwheelBladesName);
                if (child == null) continue;
                var item = new PinwheelBlades
                {
                    InstanceId = view.InstanceId, Blades = child, Rest = child.rotation,
                    Axis = -GridSpace3D.Direction(Vector2Int.down)   // North: clockwise as seen from the South
                };
                pinwheelBlades.Add(item);
                payload.Pinwheels.Add(item);
                ApplyPinwheel(item);
                (blades ??= new List<Transform>()).Add(child);
            }
            if (blades == null) return null;
            var batched = new List<GameObject>();
            foreach (var filter in payload.Root.GetComponentsInChildren<MeshFilter>(true))
            {
                var keep = true;
                foreach (var b in blades)
                    if (filter.transform == b || filter.transform.IsChildOf(b)) keep = false;
                if (keep) batched.Add(filter.gameObject);
            }
            return batched.ToArray();
        }

        private void ReleaseChunkPinwheels(ChunkPayload payload)
        {
            foreach (var item in payload.Pinwheels) pinwheelBlades.Remove(item);
            payload.Pinwheels.Clear();
        }

        private void ApplyPinwheel(PinwheelBlades item)
        {
            if (item.Blades == null) return;
            var angle = PinwheelAngle(item.InstanceId);
            item.Blades.rotation = Quaternion.AngleAxis(angle, item.Axis) * item.Rest;
        }

        private void OnPinwheelInteracted(SharedObjectInstance target, string message)
        {
            if (target == null || target.DefinitionId != PinwheelDefinitionId || session == null || session.World != session.OutdoorWorld) return;
            StartPinwheel(target.InstanceId, WaterClock());
        }

        // One spin of this pinwheel's blades. False (nothing new) while they are still turning.
        public bool StartPinwheel(string instanceId, float now)
        {
            if (!pinwheelSpins.TryGetValue(instanceId, out var spin)) pinwheelSpins[instanceId] = spin = new PinwheelSpin();
            if (spin.Active)
            {
                PinwheelRetriggersIgnored++;
                return false;
            }
            spin.StartedAt = now;
            spin.StartAngle = spin.Angle % 360f;
            spin.Angle = spin.StartAngle;
            spin.Active = true;
            PinwheelSpinsStarted++;
            return true;
        }

        // Every frame in play (the checks call it): the turning blades.
        public void TickPinwheels(float now)
        {
            var any = false;
            foreach (var spin in pinwheelSpins.Values)
            {
                if (!spin.Active) continue;
                var t = now - spin.StartedAt;
                if (t >= PinwheelSeconds)
                {
                    spin.Angle = spin.StartAngle + PinwheelFullTurn;
                    spin.Active = false;
                }
                else spin.Angle = spin.StartAngle + PinwheelTurn(t);
                any = true;
            }
            if (!any) return;
            foreach (var item in pinwheelBlades) ApplyPinwheel(item);
        }
    }
}
