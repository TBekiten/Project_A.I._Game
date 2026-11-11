using System.Linq;
using AIGame.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AIGame.Editor
{
    /// <summary>
    /// "A.I. > Prototip sahnesini oluştur" menüsü: boş bir sahneye yalnızca PrototypeGame bileşenini koyar,
    /// sahneyi kaydeder ve Build Settings'e ekler. Şehir, kamera ve arayüz çalışma anında koddan kurulur.
    /// </summary>
    public static class PrototypeSceneCreator
    {
        private const string ScenePath = "Assets/_Project/Scenes/Prototype.unity";

        [MenuItem("A.I./Prototip sahnesini oluştur", priority = 0)]
        public static void CreatePrototypeScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            if (!AssetDatabase.IsValidFolder("Assets/_Project/Scenes"))
                AssetDatabase.CreateFolder("Assets/_Project", "Scenes");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var go = new GameObject("Prototip Oyunu");
            go.AddComponent<PrototypeGame>();
            EditorSceneManager.SaveScene(scene, ScenePath);

            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.All(s => s.path != ScenePath))
            {
                scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }

            Debug.Log("Prototip sahnesi oluşturuldu: " + ScenePath + ". Play tuşuna basarak başlatabilirsin.");
        }

        [MenuItem("A.I./Prototip sahnesini aç", priority = 1)]
        public static void OpenPrototypeScene()
        {
            if (!System.IO.File.Exists(ScenePath))
            {
                CreatePrototypeScene();
                return;
            }
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath);
        }
    }
}
