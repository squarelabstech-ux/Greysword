using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

[InitializeOnLoad]
public class AutoSetupBossAnimator
{
    static AutoSetupBossAnimator()
    {
        EditorApplication.delayCall += DoSetup;
    }

    static void DoSetup()
    {
        ConfigureSprites();
        SetupController("Assets/Stylized3DMonster/Monster10/Anim/Monster10_AC.controller");
        SetupController("Assets/Stylized3DMonster/Monster10/Anim/InPlace_Anim/Monster10_AC_InPlace.controller");
        SetupSceneComponents();
    }

    static void ConfigureSprites()
    {
        // Automatically make sure GUI Parts PNG files are imported as Sprite (2D and UI)
        string[] guids = AssetDatabase.FindAssets("t:Texture", new[] { "Assets/GUI_Parts" });
        bool changed = false;
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.SaveAndReimport();
                changed = true;
                Debug.Log($"[AutoSetupBossAnimator] Converted {path} to Sprite (2D and UI) type.");
            }
        }
        if (changed)
        {
            AssetDatabase.Refresh();
        }
    }

    static void SetupController(string path)
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (controller == null) return;

        bool changed = false;

        // Ensure parameters exist
        changed |= EnsureParameter(controller, "IsWalking", AnimatorControllerParameterType.Bool);
        changed |= EnsureParameter(controller, "IsChasing", AnimatorControllerParameterType.Bool);
        changed |= EnsureParameter(controller, "IsAttacking", AnimatorControllerParameterType.Bool);
        changed |= EnsureParameter(controller, "IsDead", AnimatorControllerParameterType.Bool);

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;

        // Find existing states
        AnimatorState idleState = FindState(stateMachine, "Monster10_Idle") ?? FindStateWithKeyword(stateMachine, "idle");
        AnimatorState walkState = FindState(stateMachine, "Monster10_Walk") ?? FindState(stateMachine, "Monster10_Walk_InPlace") ?? FindStateWithKeyword(stateMachine, "walk");
        AnimatorState runState = FindState(stateMachine, "Monster10_Run") ?? FindState(stateMachine, "Monster10_Run_InPlace") ?? FindStateWithKeyword(stateMachine, "run");
        AnimatorState attackState = FindState(stateMachine, "Monster10_Attack01") ?? FindState(stateMachine, "Monster10_Attack01_InPlace") ?? FindStateWithKeyword(stateMachine, "attack");
        AnimatorState dieState = FindState(stateMachine, "Monster10_Die") ?? FindStateWithKeyword(stateMachine, "die") ?? FindStateWithKeyword(stateMachine, "dead");
        AnimatorState summonState = FindState(stateMachine, "bosssummon") ?? FindStateWithKeyword(stateMachine, "summon");

        // If summonState doesn't exist, let's create it using Attack04 or another clip
        if (summonState == null)
        {
            AnimationClip summonClip = FindAnimationClip("bosssummon") ?? FindAnimationClip("Monster10_Attack04");
            if (summonClip != null)
            {
                summonState = stateMachine.AddState("bosssummon");
                summonState.motion = summonClip;
                changed = true;
                Debug.Log($"[AutoSetupBossAnimator] Created 'bosssummon' state using clip: {summonClip.name}");
            }
        }

        // Setup transitions
        if (idleState != null && walkState != null)
        {
            if (!HasTransition(idleState, walkState))
            {
                var t = idleState.AddTransition(walkState);
                t.AddCondition(AnimatorConditionMode.If, 0, "IsWalking");
                t.hasExitTime = false;
                t.duration = 0.2f;
                changed = true;
            }
            if (!HasTransition(walkState, idleState))
            {
                var t = walkState.AddTransition(idleState);
                t.AddCondition(AnimatorConditionMode.IfNot, 0, "IsWalking");
                t.hasExitTime = false;
                t.duration = 0.2f;
                changed = true;
            }
        }

        if (idleState != null && runState != null)
        {
            if (!HasTransition(idleState, runState))
            {
                var t = idleState.AddTransition(runState);
                t.AddCondition(AnimatorConditionMode.If, 0, "IsChasing");
                t.hasExitTime = false;
                t.duration = 0.2f;
                changed = true;
            }
            if (!HasTransition(runState, idleState))
            {
                var t = runState.AddTransition(idleState);
                t.AddCondition(AnimatorConditionMode.IfNot, 0, "IsChasing");
                t.hasExitTime = false;
                t.duration = 0.2f;
                changed = true;
            }
        }

        if (walkState != null && runState != null)
        {
            if (!HasTransition(walkState, runState))
            {
                var t = walkState.AddTransition(runState);
                t.AddCondition(AnimatorConditionMode.If, 0, "IsChasing");
                t.hasExitTime = false;
                t.duration = 0.2f;
                changed = true;
            }
            if (!HasTransition(runState, walkState))
            {
                var t = runState.AddTransition(walkState);
                t.AddCondition(AnimatorConditionMode.IfNot, 0, "IsChasing");
                t.hasExitTime = false;
                t.duration = 0.2f;
                changed = true;
            }
        }

        if (attackState != null)
        {
            if (!HasAnyStateTransition(stateMachine, attackState))
            {
                var t = stateMachine.AddAnyStateTransition(attackState);
                t.AddCondition(AnimatorConditionMode.If, 0, "IsAttacking");
                t.hasExitTime = false;
                t.duration = 0.1f;
                changed = true;
            }
            if (attackState.transitions.Length == 0)
            {
                var t = attackState.AddExitTransition();
                t.hasExitTime = true;
                t.exitTime = 0.9f;
                t.duration = 0.2f;
                changed = true;
            }
        }

        if (dieState != null)
        {
            if (!HasAnyStateTransition(stateMachine, dieState))
            {
                var t = stateMachine.AddAnyStateTransition(dieState);
                t.AddCondition(AnimatorConditionMode.If, 0, "IsDead");
                t.hasExitTime = false;
                t.duration = 0.1f;
                changed = true;
            }
        }

        if (summonState != null && idleState != null)
        {
            stateMachine.defaultState = summonState;

            if (!HasTransition(summonState, idleState))
            {
                var t = summonState.AddTransition(idleState);
                t.hasExitTime = true;
                t.exitTime = 0.9f;
                t.duration = 0.2f;
                changed = true;
            }
        }

        if (changed)
        {
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            Debug.Log($"[AutoSetupBossAnimator] Animator controller at {path} updated successfully.");
        }
    }

    static void SetupSceneComponents()
    {
        // 1. Rig GameUIManager on Managers Object
        GameObject managersGO = GameObject.Find("GameManagers") ?? GameObject.Find("GameDirector") ?? new GameObject("GameManagers");
        
        bool changed = false;

        GameUIManager ui = managersGO.GetComponent<GameUIManager>();
        if (ui == null)
        {
            ui = managersGO.AddComponent<GameUIManager>();
            changed = true;
            Debug.Log("[AutoSetupBossAnimator] Added GameUIManager to " + managersGO.name);
        }

        // Auto-assign GUI Parts textures to GameUIManager properties
        if (ui != null)
        {
            Sprite hpFrame = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/GUI_Parts/Gui_parts/Hp_frame.png");
            Sprite hpLine = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/GUI_Parts/Gui_parts/Hp_line.png");
            Sprite progressFrame = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/GUI_Parts/Gui_parts/name_bar.png");
            Sprite buttonBackground = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/GUI_Parts/Gui_parts/button.png");
            Sprite buttonBackgroundActive = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/GUI_Parts/Gui_parts/button_ready_on.png");
            Sprite frameBig = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/GUI_Parts/Gui_parts/Frame_big.png");
            Sprite frameMid = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/GUI_Parts/Gui_parts/Frame_mid.png");
            Sprite nameBar = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/GUI_Parts/Gui_parts/name_bar3.png");

            Sprite weaponIcon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/GUI_Parts/Icons/weapon_icon.png");
            Sprite skillIcon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/GUI_Parts/Icons/skill_icon_01.png");
            Sprite armorIcon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/GUI_Parts/Icons/armor_icon.png");
            Sprite skullIcon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/GUI_Parts/Icons/stoune_icon.png");

            if (ui.hpFrame != hpFrame || ui.hpLine != hpLine || ui.progressFrame != progressFrame ||
                ui.buttonBackground != buttonBackground || ui.buttonBackgroundActive != buttonBackgroundActive ||
                ui.frameBig != frameBig || ui.frameMid != frameMid || ui.nameBar != nameBar ||
                ui.weaponIcon != weaponIcon || ui.skillIcon != skillIcon || ui.armorIcon != armorIcon || ui.skullIcon != skullIcon)
            {
                ui.hpFrame = hpFrame;
                ui.hpLine = hpLine;
                ui.progressFrame = progressFrame;
                ui.progressLine = hpLine;
                ui.buttonBackground = buttonBackground;
                ui.buttonBackgroundActive = buttonBackgroundActive;
                ui.frameBig = frameBig;
                ui.frameMid = frameMid;
                ui.nameBar = nameBar;
                ui.weaponIcon = weaponIcon;
                ui.skillIcon = skillIcon;
                ui.armorIcon = armorIcon;
                ui.skullIcon = skullIcon;
                changed = true;
                EditorUtility.SetDirty(ui);
                Debug.Log("[AutoSetupBossAnimator] Assigned all sprites on GameUIManager components in scene.");
            }
        }

        // 2. Rig BossArena GameObject
        GameObject bossArenaGO = GameObject.Find("BossArena");
        if (bossArenaGO == null)
        {
            var roots = SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (var r in roots)
            {
                if (r.name.IndexOf("BossArena", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    bossArenaGO = r;
                    break;
                }
                var t = r.transform.Find("BossArena");
                if (t != null)
                {
                    bossArenaGO = t.gameObject;
                    break;
                }
            }
        }

        if (bossArenaGO != null)
        {
            BossArena bossArenaComp = bossArenaGO.GetComponent<BossArena>();
            if (bossArenaComp == null)
            {
                bossArenaComp = bossArenaGO.AddComponent<BossArena>();
                changed = true;
                Debug.Log("[AutoSetupBossAnimator] Added BossArena script component to BossArena GameObject in scene.");
            }
        }

        // 3. Rig Monster10 GameObject
        GameObject monster10GO = GameObject.Find("Monster10");
        if (monster10GO == null)
        {
            var roots = SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (var r in roots)
            {
                var transforms = r.GetComponentsInChildren<Transform>(true);
                foreach (var t in transforms)
                {
                    if (t.name.IndexOf("Monster10", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        monster10GO = t.gameObject;
                        break;
                    }
                }
                if (monster10GO != null) break;
            }
        }

        if (monster10GO != null)
        {
            Animator anim = monster10GO.GetComponent<Animator>();
            if (anim == null)
            {
                anim = monster10GO.GetComponentInChildren<Animator>();
            }

            if (anim != null)
            {
                if (anim.runtimeAnimatorController == null)
                {
                    var ac = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Stylized3DMonster/Monster10/Anim/Monster10_AC.controller");
                    if (ac != null)
                    {
                        anim.runtimeAnimatorController = ac;
                        changed = true;
                        Debug.Log("[AutoSetupBossAnimator] Assigned Monster10_AC controller to Monster10 Animator component.");
                    }
                }

                if (anim.avatar == null)
                {
                    Avatar avatar = FindMonster10Avatar();
                    if (avatar != null)
                    {
                        anim.avatar = avatar;
                        changed = true;
                        Debug.Log($"[AutoSetupBossAnimator] Loaded and assigned Avatar '{avatar.name}' to Monster10 Animator component.");
                    }
                }
            }

            CapsuleCollider col = monster10GO.GetComponent<CapsuleCollider>();
            if (col == null)
            {
                col = monster10GO.AddComponent<CapsuleCollider>();
                col.center = new Vector3(0f, 1.5f, 0f);
                col.radius = 1.2f;
                col.height = 3.5f;
                changed = true;
                Debug.Log("[AutoSetupBossAnimator] Added CapsuleCollider component to Monster10 in scene.");
            }

            UnityEngine.AI.NavMeshAgent agent = monster10GO.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent == null)
            {
                agent = monster10GO.AddComponent<UnityEngine.AI.NavMeshAgent>();
                agent.speed = 4.5f;
                agent.acceleration = 15f;
                agent.angularSpeed = 360f;
                agent.stoppingDistance = 2.5f;
                changed = true;
                Debug.Log("[AutoSetupBossAnimator] Added NavMeshAgent component to Monster10 in scene.");
            }

            ZombieHealth health = monster10GO.GetComponent<ZombieHealth>();
            if (health == null)
            {
                health = monster10GO.AddComponent<ZombieHealth>();
                health.maxHealth = 600f;
                changed = true;
                Debug.Log("[AutoSetupBossAnimator] Added ZombieHealth component to Monster10 in scene.");
            }

            ZombieAI ai = monster10GO.GetComponent<ZombieAI>();
            if (ai == null)
            {
                ai = monster10GO.AddComponent<ZombieAI>();
                ai.detectionRange = 150f;
                ai.chaseRange = 150f;
                ai.attackDamage = 25f;
                ai.attackRange = 3f;
                changed = true;
                Debug.Log("[AutoSetupBossAnimator] Added ZombieAI component to Monster10 in scene.");
            }

            if (monster10GO.activeSelf)
            {
                monster10GO.SetActive(false);
                changed = true;
                Debug.Log("[AutoSetupBossAnimator] Deactivated Monster10 in scene so it is ready for ritual summon.");
            }
        }

        if (changed && !Application.isPlaying)
        {
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }
    }

    static bool EnsureParameter(AnimatorController controller, string name, AnimatorControllerParameterType type)
    {
        foreach (var param in controller.parameters)
        {
            if (param.name == name) return false;
        }
        controller.AddParameter(name, type);
        return true;
    }

    static AnimatorState FindState(AnimatorStateMachine stateMachine, string name)
    {
        foreach (var state in stateMachine.states)
        {
            if (state.state.name == name) return state.state;
        }
        return null;
    }

    static AnimatorState FindStateWithKeyword(AnimatorStateMachine stateMachine, string keyword)
    {
        foreach (var state in stateMachine.states)
        {
            if (state.state.name.IndexOf(keyword, System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return state.state;
            }
        }
        return null;
    }

    static bool HasTransition(AnimatorState src, AnimatorState dst)
    {
        foreach (var t in src.transitions)
        {
            if (t.destinationState == dst) return true;
        }
        return false;
    }

    static bool HasAnyStateTransition(AnimatorStateMachine stateMachine, AnimatorState dst)
    {
        foreach (var t in stateMachine.anyStateTransitions)
        {
            if (t.destinationState == dst) return true;
        }
        return false;
    }

    static AnimationClip FindAnimationClip(string name)
    {
        string[] guids = AssetDatabase.FindAssets(name + " t:AnimationClip");
        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        }
        return null;
    }

    static Avatar FindMonster10Avatar()
    {
        string fbxPath = "Assets/Stylized3DMonster/Monster10/Monster10.fbx";
        var assets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
        foreach (var asset in assets)
        {
            if (asset is Avatar avatar)
            {
                return avatar;
            }
        }
        return null;
    }
}
