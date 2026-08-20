using UnityEngine;
using DG.Tweening;

public enum ItemType
{
    Coin,
    Lighting,
    Boost,
    Barrier
}

public class CollectibleItem : MonoBehaviour
{
    [Header("Item Properties")]
    public ItemType itemType;
    public int scoreModifier;
    public float speedBoostDuration = 2f;
    public string popupName;

    public bool isCollected = false;
    [Tooltip("Is this a bonus item spawned by lightning/mechanics (should not be replaced upon collection)")]
    public bool isBonusSpawn = false;

    public void Initialize(ItemType type, int scoreMod, float boostDur, string popup)
    {
        this.itemType = type;
        this.scoreModifier = scoreMod;
        this.speedBoostDuration = boostDur;
        this.popupName = popup;
    }

    private void Start()
    {
        // Force all colliders on the item to be triggers to prevent physical blocking of the player
        foreach (var col in GetComponentsInChildren<Collider>())
        {
            col.isTrigger = true;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        CheckAndCollect(collision.gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        CheckAndCollect(other.gameObject);
    }

    public void CheckAndCollect(GameObject otherObj)
    {
        if (isCollected) return;

        // Check if the colliding object is the player using multiple robust checks:
        // 1. Has SimplePlayerController
        var player = otherObj.GetComponentInParent<SimplePlayerController>();
        if (player == null)
        {
            player = otherObj.GetComponent<SimplePlayerController>();
        }

        // 2. Has PlayerIdentity (matches original EnemyCube logic)
        if (player == null)
        {
            var identity = otherObj.GetComponentInParent<PlayerIdentity>();
            if (identity == null)
            {
                identity = otherObj.GetComponent<PlayerIdentity>();
            }
            if (identity != null)
            {
                player = identity.GetComponent<SimplePlayerController>();
                if (player == null)
                {
                    player = FindObjectOfType<SimplePlayerController>();
                }
            }
        }

        // 3. GameObject name check (case-insensitive check for "player")
        if (player == null)
        {
            if (otherObj.name.ToLower() == "player" || (otherObj.transform.parent != null && otherObj.transform.parent.name.ToLower() == "player"))
            {
                player = FindObjectOfType<SimplePlayerController>();
            }
        }

        if (player != null)
        {
            Collect(player);
        }
    }

    private void Collect(SimplePlayerController player)
    {
        isCollected = true;

        // Instantly disable all colliders and renderers on the item to disappear immediately
        foreach (var col in GetComponentsInChildren<Collider>())
        {
            col.enabled = false;
        }
        foreach (var rend in GetComponentsInChildren<Renderer>())
        {
            rend.enabled = false;
        }

        // 1. Apply speed boost if applicable
        if (itemType == ItemType.Boost && player != null)
        {
            player.ApplySpeedBoost(speedBoostDuration);
        }

        // 1.5 Play SFX based on item type
        if (AudioManager.Instance != null)
        {
            switch (itemType)
            {
                case ItemType.Coin:
                    AudioManager.Instance.PlaySFX(SFXType.Coin);
                    break;
                case ItemType.Lighting:
                    AudioManager.Instance.PlaySFX(SFXType.Lightning);
                    break;
                case ItemType.Boost:
                    AudioManager.Instance.PlaySFX(SFXType.BoostItem);
                    break;
                case ItemType.Barrier:
                    AudioManager.Instance.PlaySFX(SFXType.Obstacle);
                    break;
            }
        }

        // 2. Delegate mechanics (scoring, streaks, timers, lightning, popup triggers)
        if (GameMechanicsManager.Instance != null)
        {
            GameMechanicsManager.Instance.OnPlayerCollidedWithItem(this);
        }
        else
        {
            // Fallback to original score calculation if GameMechanicsManager is not present
            if (GameFlowController.Instance != null)
            {
                GameFlowController.Instance.AddScore(scoreModifier);
            }
        }

        // 3. Notify spawner of collection
        if (EnemySpawner.Instance != null)
        {
            EnemySpawner.Instance.OnItemCollected(gameObject, itemType, popupName);
        }

        // 4. Clean up the object (already hidden, destroy it)
        transform.DOKill();
        Destroy(gameObject);
    }
}
