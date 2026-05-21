using UnityEngine;

/// <summary>
/// WeaponPickup — Attaches to the sword object in the world.
/// Needs a Collider with IsTrigger = true.
/// </summary>
public class WeaponPickup : MonoBehaviour
{
    [Header("Equip Settings")]
    [Tooltip("Name of the bone to attach the weapon to")]
    public string targetHandSlotName = "PT_Medieval_Male_Right_Hand_Weapon_slot";

    [Tooltip("Exact coordinates provided by user for perfect alignment")]
    public Vector3 equipPosition = new Vector3(0.016f, 0.003f, -0.017f);
    public Vector3 equipRotation = new Vector3(17.448f, 212.877f, 195.796f);
    public Vector3 equipScale = new Vector3(0.6f, 0.6f, 0.6f);

    private bool isEquipped = false;

    void OnTriggerEnter(Collider other)
    {
        if (isEquipped) return;

        if (other.CompareTag("Player"))
        {
            EquipWeapon(other.transform);
        }
    }

    void EquipWeapon(Transform playerTransform)
    {
        isEquipped = true;

        // Recursively find the exact weapon slot deep in the player's bones
        Transform handSlot = FindChildRecursive(playerTransform, targetHandSlotName);

        if (handSlot == null)
        {
            Debug.LogError($"[WeaponPickup] Could not find hand slot '{targetHandSlotName}' on player!");
            return;
        }

        // 1. Parent the sword to the hand slot
        transform.SetParent(handSlot);

        // 2. Apply the exact local coordinates to perfectly align it
        transform.localPosition = equipPosition;
        transform.localEulerAngles = equipRotation;
        transform.localScale = equipScale;

        // 3. Disable the trigger collider so it doesn't fire again
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        // 4. Tell PlayerCombat they are now armed!
        PlayerCombat combat = playerTransform.GetComponent<PlayerCombat>();
        if (combat != null)
        {
            combat.hasWeapon = true;
            Debug.Log("[WeaponPickup] Player equipped the Sword! Combat unlocked.");
        }
    }

    Transform FindChildRecursive(Transform parent, string name)
    {
        if (parent.name == name) return parent;
        foreach (Transform child in parent)
        {
            Transform result = FindChildRecursive(child, name);
            if (result != null) return result;
        }
        return null;
    }
}
