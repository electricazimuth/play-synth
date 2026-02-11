using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;


public class SceneHelper : MonoBehaviour{

	public void LoadNextScene(){
        int totalScenes = SceneManager.sceneCountInBuildSettings;
        Scene scene = SceneManager.GetActiveScene();
        int currentIndex = scene.buildIndex;
        int nextScene = 0;
        if(currentIndex + 1 < totalScenes){
            nextScene = currentIndex + 1;
        }
        SceneManager.LoadSceneAsync(nextScene);

    }

    public void RestartScene(){
        //Debug.Log("Restart Scene");
        //int totalScenes = SceneManager.sceneCountInBuildSettings;
        Scene scene = SceneManager.GetActiveScene();
        int currentIndex = scene.buildIndex;
        SceneManager.LoadScene(currentIndex);

    }
    public void QuitScene(){
        
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
            Debug.Log("Quit Scene");
        #elif UNITY_WEBPLAYER
            Application.OpenURL("http://google.com");
        #else
            Application.Quit();
        #endif
    }

}
