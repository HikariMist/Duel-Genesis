#if UNITY_EDITOR
using DuelGenesis.Dueling;
using DuelGenesis.Economy;
using DuelGenesis.Interaction;
using DuelGenesis.Player;
using DuelGenesis.Shops;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DuelGenesis.EditorTools
{
    public static class GenesisPrototypeBuilder
    {
        private const string ScenePath = "Assets/Scenes/GenesisPrototype.unity";

        [MenuItem("Duel Genesis/Build First Playable Prototype")]
        public static void BuildPrototype()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateLighting();
            GameObject player = CreatePlayer();
            CreateCamera(player.transform);
            CreateGround();
            CreateCardShop();
            CreateDuelTable();
            CreateInteractionPrompt(player);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = player;

            Debug.Log("Duel: Genesis prototype created at " + ScenePath + ". Press Play and use WASD + mouse. E interacts, Q leaves a duel seat.");
        }

        private static void CreateLighting()
        {
            GameObject sun = new GameObject("Sun");
            Light light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            sun.transform.rotation = Quaternion.Euler(50f, -35f, 0f);

            RenderSettings.ambientLight = new Color(0.45f, 0.5f, 0.6f);
        }

        private static GameObject CreatePlayer()
        {
            GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player_Hikari_Blockout";
            player.transform.position = new Vector3(0f, 1f, -7f);

            Object.DestroyImmediate(player.GetComponent<CapsuleCollider>());
            CharacterController controller = player.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.radius = 0.45f;
            controller.center = new Vector3(0f, 1f, 0f);

            player.AddComponent<ThirdPersonPlayerController>();
            player.AddComponent<PlayerInteractor>();
            player.AddComponent<GenesisWallet>();

            return player;
        }

        private static void CreateCamera(Transform player)
        {
            GameObject camObject = new GameObject("Main Camera");
            camObject.tag = "MainCamera";
            Camera camera = camObject.AddComponent<Camera>();
            camera.fieldOfView = 60f;
            camObject.AddComponent<AudioListener>();

            ThirdPersonCamera follow = camObject.AddComponent<ThirdPersonCamera>();
            follow.target = player;
            camObject.transform.position = player.position + new Vector3(0f, 3f, -6f);
        }

        private static void CreateGround()
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Genesis_City_Test_Ground";
            ground.transform.localScale = new Vector3(4f, 1f, 4f);
        }

        private static void CreateCardShop()
        {
            GameObject shop = new GameObject("Genesis Card Shop Prototype");
            shop.transform.position = new Vector3(-6f, 0f, 2f);

            GameObject building = GameObject.CreatePrimitive(PrimitiveType.Cube);
            building.name = "Shop Building";
            building.transform.SetParent(shop.transform);
            building.transform.localPosition = new Vector3(0f, 2f, 1.5f);
            building.transform.localScale = new Vector3(6f, 4f, 4f);

            GameObject terminal = GameObject.CreatePrimitive(PrimitiveType.Cube);
            terminal.name = "Pack Terminal - 1000 GC";
            terminal.transform.SetParent(shop.transform);
            terminal.transform.localPosition = new Vector3(0f, 1f, -1f);
            terminal.transform.localScale = new Vector3(2f, 2f, 0.7f);
            terminal.AddComponent<CardShopTerminal>();
        }

        private static void CreateDuelTable()
        {
            GameObject root = new GameObject("Duel Table Prototype");
            root.transform.position = new Vector3(6f, 0f, 2f);

            GameObject table = GameObject.CreatePrimitive(PrimitiveType.Cube);
            table.name = "Tabletop Arena Blockout";
            table.transform.SetParent(root.transform);
            table.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            table.transform.localScale = new Vector3(4.8f, 0.25f, 3f);

            CreateArenaRail(root.transform, "Blue Rail", new Vector3(-2.15f, 1.55f, 0f), new Vector3(0.25f, 0.55f, 3.1f));
            CreateArenaRail(root.transform, "Red Rail", new Vector3(2.15f, 1.55f, 0f), new Vector3(0.25f, 0.55f, 3.1f));
            CreateArenaRail(root.transform, "Back Rail", new Vector3(0f, 1.55f, 1.35f), new Vector3(4.6f, 0.55f, 0.25f));
            CreateArenaRail(root.transform, "Front Rail", new Vector3(0f, 1.55f, -1.35f), new Vector3(4.6f, 0.55f, 0.25f));

            GameObject seatTrigger = GameObject.CreatePrimitive(PrimitiveType.Cube);
            seatTrigger.name = "Seat Interaction";
            seatTrigger.transform.SetParent(root.transform);
            seatTrigger.transform.localPosition = new Vector3(0f, 0.6f, -2.35f);
            seatTrigger.transform.localScale = new Vector3(1.5f, 1.2f, 0.8f);

            DuelTableSeat seat = seatTrigger.AddComponent<DuelTableSeat>();
            seat.seatPoint = CreatePoint(root.transform, "Seat Point", new Vector3(0f, 0f, -2.25f), Quaternion.Euler(0f, 0f, 0f));
            seat.standPoint = CreatePoint(root.transform, "Stand Point", new Vector3(0f, 0f, -3.3f), Quaternion.identity);
        }

        private static void CreateArenaRail(Transform parent, string name, Vector3 position, Vector3 scale)
        {
            GameObject rail = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rail.name = name;
            rail.transform.SetParent(parent);
            rail.transform.localPosition = position;
            rail.transform.localScale = scale;
        }

        private static Transform CreatePoint(Transform parent, string name, Vector3 localPosition, Quaternion localRotation)
        {
            GameObject point = new GameObject(name);
            point.transform.SetParent(parent);
            point.transform.localPosition = localPosition;
            point.transform.localRotation = localRotation;
            return point.transform;
        }

        private static void CreateInteractionPrompt(GameObject player)
        {
            GameObject canvasObject = new GameObject("Interaction Prompt Canvas");
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            CanvasGroup group = canvasObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;

            GameObject textObject = new GameObject("Interaction Prompt");
            textObject.transform.SetParent(canvasObject.transform, false);
            Text text = textObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 28;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.text = "[E] Interact";

            RectTransform rect = text.rectTransform;
            rect.anchorMin = new Vector2(0.25f, 0.08f);
            rect.anchorMax = new Vector2(0.75f, 0.15f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            PlayerInteractor interactor = player.GetComponent<PlayerInteractor>();
            interactor.promptCanvas = group;
            interactor.promptText = text;
        }
    }
}
#endif
