// Collision positions and normals already use Unity space. Admission precedes each child birth.
using System.Collections.Generic;
using UnityEngine;
namespace NovaStriker.Game {
 public sealed class WeatherSplash : MonoBehaviour {
  public ParticleSystem source, splash;
  readonly List<ParticleCollisionEvent> collisions = new List<ParticleCollisionEvent>(128);
  void OnParticleCollision(GameObject other) {
   if (!NovaStriker.Sim.Cfg.SETTINGS.weather || !ParticleBudget.Advancing || !source || !splash) return;
   int count = source.GetCollisionEvents(other, collisions);
   for (int i = 0; i < Mathf.Min(count, 32); i++) {
    var hit = collisions[i];
    for (int j = 0; j < 2; j++) {
     if (!ParticleBudget.Admit(true)) return;
     Vector3 normal = hit.normal.normalized;
     var particle = new ParticleSystem.EmitParams {
      position = hit.intersection + normal * 0.015f,
      velocity = normal * Random.Range(0.8f, 2.2f) + Vector3.ProjectOnPlane(Random.insideUnitSphere, normal) * 0.7f,
      startLifetime = Random.Range(0.18f, 0.32f)
     };
     splash.Emit(particle, 1);
    }
   }
  }
 }
}
