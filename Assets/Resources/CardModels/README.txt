Duel: Genesis production model convention

Place model prefabs in this folder using the card ID as the prefab name:

Assets/Resources/CardModels/<CARD_ID>.prefab

Example:
Assets/Resources/CardModels/EXAMPLE001.prefab

At runtime, CardModelRegistry.LoadPrefab("EXAMPLE001") will find that prefab automatically.

Alternatively, an imported card JSON record can specify:
"modelResource": "CardModels/MyCustomPrefabName"

Only use card art, model files, audio, and other content that you have the rights or permission to use.
