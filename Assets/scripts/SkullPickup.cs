using UnityEngine;

/// <summary>
/// Attach this to the Skull prefab (or any collectible).
/// Requires a Collider with IsTrigger = true.
/// </summary>
public class SkullPickup : MonoBehaviour
{
    [Tooltip("Rotation speed of the skull collectible")]
    public float spinSpeed = 90f;
    [Tooltip("Bobbing speed")]
    public float bobSpeed = 2f;
    [Tooltip("Bobbing height")]
    public float bobHeight = 0.5f;

    private Vector3 startPos;

    void Start()
    {
        startPos = transform.position;
    }

    void Update()
    {
        // Simple idle animation: spin and bob up and down
        transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime);
        transform.position = startPos + new Vector3(0f, Mathf.Sin(Time.time * bobSpeed) * bobHeight, 0f);
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (SkullCounter.Instance != null)
            {
                SkullCounter.Instance.AddSkull();
                Destroy(gameObject);
            }
        }
    }
}
