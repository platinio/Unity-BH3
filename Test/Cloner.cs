using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Cloner : MonoBehaviour
{
   public GameObject prefab;
   public int amount;

   private void Start()
   {
      for (int i = 0; i < amount; i++)
      {
         Instantiate(prefab);
      }
   }
}
