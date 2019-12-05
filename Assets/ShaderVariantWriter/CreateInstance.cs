using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CreateInstance : MonoBehaviour
{
    public GameObject prefab;

    void Start()
    {
        Instantiate(prefab);
    }
}
