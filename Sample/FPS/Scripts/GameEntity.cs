using System.Collections;
using System.Collections.Generic;
using Platinio;
using Platinio.FactionSystem;
using UnityEngine;
using UnityEngine.AI;

public class GameEntity : MonoBehaviour
{
    [SerializeField] private Faction faction;
    [SerializeField] private EntityNavAgent entityNavAgent;

    public Faction Faction => faction;
    public EntityNavAgent EntityNavAgent => entityNavAgent;
}
