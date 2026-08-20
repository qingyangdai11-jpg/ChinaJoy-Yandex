using UnityEngine;
using DG.Tweening;

public class EnemyCube : MonoBehaviour
{
    private EnemySpawner spawner;
    private bool isDead = false;

    public void Initialize(EnemySpawner spawner)
    {
        this.spawner = spawner;
    }

    private void Start()
    {
        // Force all colliders on the enemy cube to be triggers to prevent physical bumps
        foreach (var col in GetComponentsInChildren<Collider>())
        {
            col.isTrigger = true;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (isDead) return;

        if (collision.gameObject.GetComponentInParent<PlayerIdentity>() != null)
        {
            OnAttacked();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isDead) return;

        if (other.gameObject.GetComponentInParent<PlayerIdentity>() != null)
        {
            OnAttacked();
        }
    }

    public void OnAttacked()
    {
        if (isDead) return;
        isDead = true;

        // Instantly disable all colliders and renderers on the cube to disappear immediately
        foreach (var col in GetComponentsInChildren<Collider>())
        {
            col.enabled = false;
        }
        foreach (var rend in GetComponentsInChildren<Renderer>())
        {
            rend.enabled = false;
        }

        // Notify spawner that this cube has been destroyed
        if (spawner != null)
        {
            spawner.OnEnemyDestroyed(gameObject);
        }

        // Add 10 points to score
        if (GameFlowController.Instance != null)
        {
            GameFlowController.Instance.AddScore(10);
        }

        // Clean up the object (already hidden, destroy it)
        Destroy(gameObject);
    }
}