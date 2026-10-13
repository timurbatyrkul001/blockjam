using UnityEngine;

namespace ColorBlockJam
{
    // Koddan kurulan kısa ömürlü küp konfeti patlaması; bitince kendini yok eder.
    public static class Confetti
    {
        static Mesh cubeMesh;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => cubeMesh = null;

        static Mesh CubeMesh
        {
            get
            {
                if (cubeMesh != null) return cubeMesh;
                var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cubeMesh = tmp.GetComponent<MeshFilter>().sharedMesh;
                Object.Destroy(tmp);
                return cubeMesh;
            }
        }

        public static void Burst(Vector3 worldPos, Color color, Vector3 direction, int count = 20)
        {
            var go = new GameObject("Confetti");
            go.transform.position = worldPos;
            var dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.up;
            go.transform.rotation = Quaternion.LookRotation((dir + Vector3.up * 1.2f).normalized);

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.3f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.85f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 6.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.17f);
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = color;
            main.gravityModifier = 2.2f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = count + 4;
            main.stopAction = ParticleSystemStopAction.Destroy;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 38f;
            shape.radius = 0.25f;

            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.separateAxes = true;
            rot.x = new ParticleSystem.MinMaxCurve(-8f, 8f);
            rot.y = new ParticleSystem.MinMaxCurve(-8f, 8f);
            rot.z = new ParticleSystem.MinMaxCurve(-8f, 8f);

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 1f), new Keyframe(0.7f, 0.9f), new Keyframe(1f, 0f)));

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Mesh;
            r.mesh = CubeMesh;
            r.sharedMaterial = Palette.Mat(color, 0.6f);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;

            ps.Play();
        }
    }
}
