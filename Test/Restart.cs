using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Restart : MonoBehaviour
{
   [SerializeField] private float restartTime;

   private float currentTime = 0;
   
   private void Update()
   {
      currentTime += Time.deltaTime;

      if (currentTime > restartTime)
      {
         SceneManager.LoadScene(0);
      }
   }
}
