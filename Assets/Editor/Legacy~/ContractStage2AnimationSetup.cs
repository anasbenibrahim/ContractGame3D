using System.IO;
using Contract.Player;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Contract.EditorTools
{
    public static class ContractStage2AnimationSetup
    {
        private const string AnimationFolder = "Assets/Art/Animations";
        private const string ControllerPath = AnimationFolder + "/PlayerLocomotion.controller";
        private const string PlayerPrefabPath = "Assets/Prefabs/Player/Player.prefab";

        [MenuItem("CONTRACT/Setup/Stage 2 Animations")]
        public static void Install()
        {
            if (!AssetDatabase.IsValidFolder(AnimationFolder))
            {
                Directory.CreateDirectory(AnimationFolder);
                AssetDatabase.Refresh();
            }

            AnimationClip idle = CreateLocomotionClip("Player_Idle", 2.4f, 0.015f, 4f, 6f, true);
            AnimationClip walk = CreateLocomotionClip("Player_Walk", 0.56f, 0.05f, 22f, 32f, true);
            AnimationClip sprint = CreateLocomotionClip("Player_Sprint", 0.36f, 0.075f, 34f, 46f, true);
            AnimationClip crouchIdle = CreateLocomotionClip("Player_CrouchIdle", 2.2f, 0.01f, 3f, 4f, true);
            AnimationClip crouchMove = CreateLocomotionClip("Player_CrouchMove", 0.7f, 0.035f, 14f, 20f, true);
            AnimationClip air = CreateAirClip();

            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null)
            {
                AssetDatabase.DeleteAsset(ControllerPath);
            }

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("VerticalSpeed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Crouch", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
            AnimatorControllerParameter[] parameters = controller.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].name == "Grounded")
                {
                    parameters[i].defaultBool = true;
                }
            }

            controller.parameters = parameters;

            BlendTree locomotion = CreateBlendTree(controller, "Locomotion", idle, walk, sprint, 0f, 0.6f, 1f);
            BlendTree crouch = CreateBlendTree(controller, "Crouch", crouchIdle, crouchMove, crouchMove, 0f, 0.35f, 1f);

            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            AnimatorState locomotionState = machine.AddState("Locomotion");
            locomotionState.motion = locomotion;
            AnimatorState crouchState = machine.AddState("Crouch");
            crouchState.motion = crouch;
            AnimatorState airState = machine.AddState("Air");
            airState.motion = air;
            machine.defaultState = locomotionState;

            AddAnyTransition(machine, locomotionState, false, true);
            AddAnyTransition(machine, crouchState, true, true);
            AddAirTransition(machine, airState);

            EditorUtility.SetDirty(controller);
            AssignToPlayerPrefab(controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[CONTRACT] Stage 2 locomotion animator assigned to the player prefab.");
        }

        private static void AddAirTransition(AnimatorStateMachine machine, AnimatorState destination)
        {
            AnimatorStateTransition transition = machine.AddAnyStateTransition(destination);
            transition.hasExitTime = false;
            transition.duration = 0.08f;
            transition.canTransitionToSelf = false;
            transition.AddCondition(AnimatorConditionMode.IfNot, 0f, "Grounded");
        }

        private static void AddAnyTransition(AnimatorStateMachine machine, AnimatorState destination, bool crouching, bool grounded)
        {
            AnimatorStateTransition transition = machine.AddAnyStateTransition(destination);
            transition.hasExitTime = false;
            transition.duration = 0.12f;
            transition.canTransitionToSelf = false;
            transition.AddCondition(
                crouching ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot,
                0f,
                "Crouch");
            transition.AddCondition(
                grounded ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot,
                0f,
                "Grounded");
        }

        private static BlendTree CreateBlendTree(
            AnimatorController controller,
            string name,
            AnimationClip idle,
            AnimationClip moving,
            AnimationClip fast,
            float idleThreshold,
            float movingThreshold,
            float fastThreshold)
        {
            var tree = new BlendTree
            {
                name = name,
                blendParameter = "Speed",
                blendType = BlendTreeType.Simple1D,
                useAutomaticThresholds = false
            };

            tree.AddChild(idle, idleThreshold);
            tree.AddChild(moving, movingThreshold);
            tree.AddChild(fast, fastThreshold);
            AssetDatabase.AddObjectToAsset(tree, controller);
            return tree;
        }

        private static AnimationClip CreateLocomotionClip(string name, float duration, float bob, float shoulderDegrees, float legDegrees, bool loop)
        {
            string path = $"{AnimationFolder}/{name}.anim";
            AnimationClip clip = LoadOrCreateClip(path, name);
            ClearCurves(clip);

            float bodyBase = 0.82f;
            float headBase = 1.62f;
            SetCurve(clip, "Visual/Body", "localPosition.y", new[]
            {
                Key(0f, bodyBase),
                Key(duration * 0.25f, bodyBase + bob),
                Key(duration * 0.5f, bodyBase),
                Key(duration * 0.75f, bodyBase + bob * 0.85f),
                Key(duration, bodyBase)
            });
            SetCurve(clip, "Visual/Head", "localPosition.y", new[]
            {
                Key(0f, headBase),
                Key(duration * 0.5f, headBase + bob * 0.35f),
                Key(duration, headBase)
            });
            SetCurve(clip, "Visual/Shoulder L", "localEulerAnglesRaw.x", new[]
            {
                Key(0f, 0f),
                Key(duration * 0.25f, shoulderDegrees),
                Key(duration * 0.5f, 0f),
                Key(duration * 0.75f, -shoulderDegrees),
                Key(duration, 0f)
            });
            SetCurve(clip, "Visual/Shoulder R", "localEulerAnglesRaw.x", new[]
            {
                Key(0f, 0f),
                Key(duration * 0.25f, -shoulderDegrees),
                Key(duration * 0.5f, 0f),
                Key(duration * 0.75f, shoulderDegrees),
                Key(duration, 0f)
            });
            SetCurve(clip, "Visual/Backpack", "localEulerAnglesRaw.x", new[]
            {
                Key(0f, 0f),
                Key(duration * 0.5f, bob * 40f),
                Key(duration, 0f)
            });
            SetCurve(clip, "Visual/Leg L", "localEulerAnglesRaw.x", new[]
            {
                Key(0f, 0f),
                Key(duration * 0.25f, legDegrees),
                Key(duration * 0.5f, 0f),
                Key(duration * 0.75f, -legDegrees),
                Key(duration, 0f)
            });
            SetCurve(clip, "Visual/Leg R", "localEulerAnglesRaw.x", new[]
            {
                Key(0f, 0f),
                Key(duration * 0.25f, -legDegrees),
                Key(duration * 0.5f, 0f),
                Key(duration * 0.75f, legDegrees),
                Key(duration, 0f)
            });

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        private static AnimationClip CreateAirClip()
        {
            const string path = AnimationFolder + "/Player_Air.anim";
            AnimationClip clip = LoadOrCreateClip(path, "Player_Air");
            ClearCurves(clip);
            SetCurve(clip, "Visual/Body", "localEulerAnglesRaw.x", new[]
            {
                Key(0f, 0f),
                Key(0.12f, -14f),
                Key(0.35f, -14f)
            });
            SetCurve(clip, "Visual/Head", "localEulerAnglesRaw.x", new[]
            {
                Key(0f, 0f),
                Key(0.12f, 8f),
                Key(0.35f, 8f)
            });
            SetCurve(clip, "Visual/Leg L", "localEulerAnglesRaw.x", new[]
            {
                Key(0f, 0f),
                Key(0.16f, 18f),
                Key(0.35f, 18f)
            });
            SetCurve(clip, "Visual/Leg R", "localEulerAnglesRaw.x", new[]
            {
                Key(0f, 0f),
                Key(0.16f, 18f),
                Key(0.35f, 18f)
            });

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        private static AnimationClip LoadOrCreateClip(string path, string clipName)
        {
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip != null)
            {
                return clip;
            }

            clip = new AnimationClip { name = clipName, frameRate = 30f };
            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }

        private static void ClearCurves(AnimationClip clip)
        {
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
            {
                AnimationUtility.SetEditorCurve(clip, binding, null);
            }
        }

        private static void SetCurve(AnimationClip clip, string path, string property, Keyframe[] keys)
        {
            var curve = new AnimationCurve(keys);
            for (int i = 0; i < curve.length; i++)
            {
                curve.SmoothTangents(i, 0f);
            }

            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), property), curve);
        }

        private static Keyframe Key(float time, float value)
        {
            return new Keyframe(time, value);
        }

        private static void AssignToPlayerPrefab(RuntimeAnimatorController runtimeController)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[CONTRACT] Player prefab is missing at {PlayerPrefabPath}.");
                return;
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                Animator animator = contents.GetComponent<Animator>();
                if (animator == null)
                {
                    animator = contents.AddComponent<Animator>();
                }

                animator.runtimeAnimatorController = runtimeController;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.updateMode = AnimatorUpdateMode.Normal;

                if (contents.GetComponent<PlayerAnimationDriver>() == null)
                {
                    contents.AddComponent<PlayerAnimationDriver>();
                }

                EnsureSilhouette(contents);
                PrefabUtility.SaveAsPrefabAsset(contents, PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private static void EnsureSilhouette(GameObject player)
        {
            Transform visual = player.transform.Find("Visual");
            if (visual == null)
            {
                return;
            }

            int playerLayer = player.layer;
            Material suit = visual.Find("Body")?.GetComponent<Renderer>()?.sharedMaterial;
            Transform shoulderLeft = visual.Find("Shoulder L");
            Transform shoulderRight = visual.Find("Shoulder R");
            EnsureLimb(shoulderLeft, PrimitiveType.Cube, "Arm", new Vector3(0f, -2.2f, 0f), new Vector3(0.7f, 4.2f, 0.45f), suit, playerLayer);
            EnsureLimb(shoulderRight, PrimitiveType.Cube, "Arm", new Vector3(0f, -2.2f, 0f), new Vector3(0.7f, 4.2f, 0.45f), suit, playerLayer);
            EnsureLimb(visual, PrimitiveType.Capsule, "Leg L", new Vector3(-0.42f, 0.36f, 0.05f), new Vector3(0.16f, 0.34f, 0.16f), suit, playerLayer);
            EnsureLimb(visual, PrimitiveType.Capsule, "Leg R", new Vector3(0.42f, 0.36f, 0.05f), new Vector3(0.16f, 0.34f, 0.16f), suit, playerLayer);
        }

        private static void EnsureLimb(
            Transform parent,
            PrimitiveType primitiveType,
            string limbName,
            Vector3 localPosition,
            Vector3 localScale,
            Material material,
            int layer)
        {
            if (parent == null || parent.Find(limbName) != null)
            {
                return;
            }

            GameObject limb = GameObject.CreatePrimitive(primitiveType);
            limb.name = limbName;
            limb.layer = layer;
            limb.transform.SetParent(parent, false);
            limb.transform.localPosition = localPosition;
            limb.transform.localRotation = Quaternion.identity;
            limb.transform.localScale = localScale;

            Collider collider = limb.GetComponent<Collider>();
            if (collider != null)
            {
                Object.DestroyImmediate(collider);
            }

            Renderer renderer = limb.GetComponent<Renderer>();
            if (renderer != null && material != null)
            {
                renderer.sharedMaterial = material;
            }
        }
    }
}
