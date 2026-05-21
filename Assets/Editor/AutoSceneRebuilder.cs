using UnityEngine;
using UnityEditor;
using UnityEngine.AI;

[InitializeOnLoad]
public class AutoSceneRebuilder
{
    static AutoSceneRebuilder()
    {
        EditorApplication.delayCall += RebuildScene;
    }

    static void RebuildScene()
    {
        if (SessionState.GetBool("SceneRebuilt", false)) return;
        SessionState.SetBool("SceneRebuilt", true);

        Debug.Log("[AutoSceneRebuilder] Starting automatic scene reconstruction...");

        // 1. Terrain is already set up and baked by the user

        // 2. Setup Player
        GameObject player = GameObject.Find("Player") ?? GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
#pragma warning disable CS0618
            var chars = Object.FindObjectsOfType<Animator>();
#pragma warning restore CS0618
            foreach (var a in chars)
            {
                if (a.name.ToLower().Contains("player") || a.name.ToLower().Contains("male"))
                {
                    player = a.gameObject;
                    break;
                }
            }
        }

        if (player != null)
        {
            Selection.activeGameObject = player;
            SceneSetupWizard.SetupPlayer();
            
            if (Camera.main != null)
            {
                ThirdPersonCamera tpc = Camera.main.GetComponent<ThirdPersonCamera>();
                if (tpc != null) tpc.target = player.transform;
            }
        }

        // 3. Setup Zombie
        GameObject zombie = GameObject.Find("Zombie") ?? GameObject.FindGameObjectWithTag("Enemy");
        if (zombie == null)
        {
#pragma warning disable CS0618
            var chars = Object.FindObjectsOfType<Animator>();
#pragma warning restore CS0618
            foreach (var a in chars)
            {
                if (a.name.ToLower().Contains("zombie") && (player == null || a.gameObject != player))
                {
                    zombie = a.gameObject;
                    break;
                }
            }
        }

        if (zombie != null)
        {
            Selection.activeGameObject = zombie;
            SceneSetupWizard.SetupZombie();
            
            if (player != null && Vector3.Distance(zombie.transform.position, player.transform.position) < 2f)
            {
                zombie.transform.position = player.transform.position + new Vector3(5f, 0f, 5f);
            }
        }

        // 4. Setup Sword
        GameObject sword = GameObject.Find("Sword") ?? GameObject.Find("Weapon");
        if (sword == null)
        {
#pragma warning disable CS0618
            var renderers = Object.FindObjectsOfType<MeshRenderer>();
#pragma warning restore CS0618
            foreach (var r in renderers)
            {
                if (r.name.ToLower().Contains("sword"))
                {
                    sword = r.gameObject;
                    break;
                }
            }
        }

        if (sword != null)
        {
            Selection.activeGameObject = sword;
            SceneSetupWizard.SetupWeapon();
            
            if (player != null)
            {
                sword.transform.position = player.transform.position + player.transform.forward * 2f;
            }
        }

        // 5. Generate Managers
        SceneSetupWizard.GenerateManagers();

        Selection.activeGameObject = null;
        Debug.Log("[AutoSceneRebuilder] Finished automatic scene reconstruction!");
    }
}
