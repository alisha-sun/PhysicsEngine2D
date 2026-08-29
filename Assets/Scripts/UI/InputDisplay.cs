using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;

public class InputDisplay : MonoBehaviour
{
    public PlayerController controller;

    public TMP_Text hostW;
    public TMP_Text hostA;
    public TMP_Text hostS;
    public TMP_Text hostD;
    public TMP_Text clientW;
    public TMP_Text clientA;
    public TMP_Text clientS;
    public TMP_Text clientD;
    void Start(){}

    void Update()
    {
        if (controller.pc_this.W.held) hostW.color = Color.yellow;
        else hostW.color = Color.white;

        if (controller.pc_this.A.held) hostA.color = Color.yellow;
        else hostA.color = Color.white;

        if (controller.pc_this.S.held) hostS.color = Color.yellow;
        else hostS.color = Color.white;

        if (controller.pc_this.D.held) hostD.color = Color.yellow;
        else hostD.color = Color.white;

        if (controller.pc_other.W.held) clientW.color = Color.yellow;
        else clientW.color = Color.white;

        if (controller.pc_other.A.held) clientA.color = Color.yellow;
        else clientA.color = Color.white;

        if (controller.pc_other.S.held) clientS.color = Color.yellow;
        else clientS.color = Color.white;

        if (controller.pc_other.D.held) clientD.color = Color.yellow;
        else clientD.color = Color.white;
    }
}
