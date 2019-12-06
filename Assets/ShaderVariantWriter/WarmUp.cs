using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class WarmUp : MonoBehaviour
{
    public ShaderVariantCollection shaders;

    void Start()
    {
        Debug.Log("Compiled shader: Start warm up");
        shaders.WarmUp();
        Debug.Log("Compiled shader: End warm up");
        SceneManager.LoadScene("SampleScene");
    }
}
