using UnityEngine;
using UnityEditor;
using UnityEngine.InputSystem;
using UnityEngine.AI;

#pragma warning disable CS0618

public class SceneSetupWizard : EditorWindow
{
    [MenuItem("Tools/Setup Selected As Player")]
    public static void SetupPlayer()
    {
        if (Selection.activeGameObject == null)
        {
            Debug.LogError("Please select the Player GameObject in the hierarchy first!");
            return;
        }

        GameObject player = Selection.activeGameObject;
        player.tag = "Player";

        // Add core components
        GetOrAddComponent<CharacterController>(player);
        PlayerInput pInput = GetOrAddComponent<PlayerInput>(player);
        
        var inputAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
        if (inputAsset != null) pInput.actions = inputAsset;
        
        // Add and configure Animator
        Animator anim = GetOrAddComponent<Animator>(player);
        var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/PlayerAnimationController.controller");
        if (controller != null) anim.runtimeAnimatorController = controller;

        // Add scripts
        PlayerMovement pm = GetOrAddComponent<PlayerMovement>(player);
        if (Camera.main != null) 
        {
            pm.cameraTransform = Camera.main.transform;
            // Make sure camera has the follow script and link it
            ThirdPersonCamera tpc = Camera.main.GetComponent<ThirdPersonCamera>();
            if (tpc == null)
                tpc = Camera.main.gameObject.AddComponent<ThirdPersonCamera>();
            tpc.target = player.transform;
        }

        GetOrAddComponent<PlayerHealth>(player);
        GetOrAddComponent<PlayerCombat>(player);

        Debug.Log($"[SceneSetupWizard] Successfully configured {player.name} as Player!", player);
    }

    [MenuItem("Tools/Setup Selected As Zombie")]
    public static void SetupZombie()
    {
        if (Selection.activeGameObject == null)
        {
            Debug.LogError("Please select the Zombie GameObject in the hierarchy first!");
            return;
        }

        GameObject zombie = Selection.activeGameObject;

        // Add physics and nav components
        GetOrAddComponent<CapsuleCollider>(zombie);
        GetOrAddComponent<NavMeshAgent>(zombie);
        
        // Add and configure Animator
        Animator anim = GetOrAddComponent<Animator>(zombie);
        var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/EnemyAI.controller");
        if (controller != null) anim.runtimeAnimatorController = controller;

        // Add scripts
        GetOrAddComponent<ZombieHealth>(zombie);
        ZombieAI ai = GetOrAddComponent<ZombieAI>(zombie);
        
        // Auto-link player if it exists
        ai.playerHealth = FindObjectOfType<PlayerHealth>();
        if (ai.playerHealth != null) ai.playerTransform = ai.playerHealth.transform;
        
        Debug.Log($"[SceneSetupWizard] Successfully configured {zombie.name} as Zombie!", zombie);
    }

    [MenuItem("Tools/Setup Selected As Weapon (Sword)")]
    public static void SetupWeapon()
    {
        if (Selection.activeGameObject == null)
        {
            Debug.LogError("Please select the Sword GameObject in the hierarchy first!");
            return;
        }

        GameObject sword = Selection.activeGameObject;
        
        // Add trigger collider
        BoxCollider col = GetOrAddComponent<BoxCollider>(sword);
        col.isTrigger = true;

        // Add kinematic rigidbody to guarantee OnTriggerEnter fires
        Rigidbody rb = GetOrAddComponent<Rigidbody>(sword);
        rb.isKinematic = true;

        // Add script
        GetOrAddComponent<WeaponPickup>(sword);

        Debug.Log($"[SceneSetupWizard] Successfully configured {sword.name} as Weapon!", sword);
    }

    [MenuItem("Tools/Generate Game Managers")]
    public static void GenerateManagers()
    {
        GameObject managers = GameObject.Find("GameManagers");
        if (managers == null)
        {
            managers = new GameObject("GameManagers");
        }

        // Core systems
        GetOrAddComponent<GameModeManager>(managers);
        GetOrAddComponent<AdaptiveDifficultyManager>(managers);
        ZombieSpawnManager spawnMgr = GetOrAddComponent<ZombieSpawnManager>(managers);
        GetOrAddComponent<SkullCounter>(managers);
        
        // AI Director systems
        GetOrAddComponent<AgenticDecisionLogger>(managers);
        GetOrAddComponent<AntigravityGameDirector>(managers);
        
        // Ritual systems
        GetOrAddComponent<RitualManager>(managers);
        GetOrAddComponent<BossSummonManager>(managers);
        GetOrAddComponent<PlayerBehaviorTracker>(managers);

        // ── Auto-create zombie prefab from existing zombie in scene ──
        if (spawnMgr.zombiePrefab == null)
        {
            // Find the existing zombie in the scene
            ZombieAI existingZombie = FindObjectOfType<ZombieAI>();
            if (existingZombie != null)
            {
                // Save as prefab
                string prefabPath = "Assets/ZombiePrefab.prefab";
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(existingZombie.gameObject, prefabPath);
                spawnMgr.zombiePrefab = prefab;
                Debug.Log($"[SceneSetupWizard] Created zombie prefab at {prefabPath}");
            }
            else
            {
                Debug.LogWarning("[SceneSetupWizard] No zombie found in scene to create prefab from. Drag a zombie into the scene first, then run this again.");
            }
        }

        // ── Auto-create spawn points around the player ──
        if (spawnMgr.spawnPoints == null || spawnMgr.spawnPoints.Length == 0)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            Vector3 center = player != null ? player.transform.position : Vector3.zero;

            GameObject spawnParent = GameObject.Find("SpawnPoints");
            if (spawnParent == null)
                spawnParent = new GameObject("SpawnPoints");

            Transform[] points = new Transform[4];
            float radius = 30f;

            for (int i = 0; i < 4; i++)
            {
                float angle = i * 90f * Mathf.Deg2Rad;
                Vector3 pos = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);

                // Snap to terrain height
                if (Terrain.activeTerrain != null)
                    pos.y = Terrain.activeTerrain.SampleHeight(pos) + Terrain.activeTerrain.transform.position.y;

                // Snap to NavMesh
                UnityEngine.AI.NavMeshHit hit;
                if (UnityEngine.AI.NavMesh.SamplePosition(pos, out hit, 20f, UnityEngine.AI.NavMesh.AllAreas))
                    pos = hit.position;

                GameObject sp = new GameObject($"SpawnPoint_{i + 1}");
                sp.transform.position = pos;
                sp.transform.parent = spawnParent.transform;
                points[i] = sp.transform;
            }

            spawnMgr.spawnPoints = points;
            Debug.Log("[SceneSetupWizard] Created 4 spawn points around the player.");
        }

        // Set reasonable defaults
        spawnMgr.maxAliveZombies = 4;
        spawnMgr.spawnInterval = 20f;
        spawnMgr.minDistanceFromPlayer = 15f;

        Debug.Log("[SceneSetupWizard] Successfully generated all GameManagers!", managers);
    }

    [MenuItem("Tools/Fix Mistakes (Cleanup Duplicates)")]
    public static void CleanupDuplicates()
    {
        // 1. Remove Player components from invisible managers
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        GameObject realPlayer = null;

        foreach(GameObject p in players)
        {
            if (p.GetComponentInChildren<SkinnedMeshRenderer>() == null && p.GetComponentInChildren<MeshRenderer>() == null)
            {
                Debug.Log($"[Cleanup] Removing accidental Player scripts from {p.name}...");
                p.tag = "Untagged";
                if (p.GetComponent<PlayerMovement>()) DestroyImmediate(p.GetComponent<PlayerMovement>());
                if (p.GetComponent<PlayerCombat>()) DestroyImmediate(p.GetComponent<PlayerCombat>());
                if (p.GetComponent<PlayerHealth>()) DestroyImmediate(p.GetComponent<PlayerHealth>());
                if (p.GetComponent<PlayerBehaviorTracker>()) DestroyImmediate(p.GetComponent<PlayerBehaviorTracker>());
                if (p.GetComponent<PlayerInput>()) DestroyImmediate(p.GetComponent<PlayerInput>());
                if (p.GetComponent<CharacterController>()) DestroyImmediate(p.GetComponent<CharacterController>());
            }
            else
            {
                realPlayer = p;
            }
        }

        // 2. Re-link Zombie AI to the real player
        if (realPlayer != null)
        {
            ZombieAI[] zombies = FindObjectsOfType<ZombieAI>();
            foreach(ZombieAI z in zombies)
            {
                z.playerHealth = realPlayer.GetComponent<PlayerHealth>();
                z.playerTransform = realPlayer.transform;
            }
        }

        // 3. Force re-rig of animators
        AutoRigAnimators.RigControllers(true);

        Debug.Log("[Cleanup] Mistakes fixed! Zombie will target the real player, and Animator arrows are re-wired.");
    }

    // Helper method to add component if missing
    private static T GetOrAddComponent<T>(GameObject go) where T : Component
    {
        T comp = go.GetComponent<T>();
        if (comp == null)
            comp = go.AddComponent<T>();
        return comp;
    }
}

#pragma warning restore CS0618
