using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public static class AreaInspectionCaptures
{
    public static void PositionForArea(int areaIndex)
    {
        GameObject player = GameObject.Find("TutorialScene/Player_Cylinder");
        if (player == null) return;

        CharacterController cc = player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        switch (areaIndex)
        {
            case 2: // Pit Corridor start looking East (+X)
                player.transform.position = new Vector3(15.0f, 0.5f, 50.0f);
                player.transform.rotation = Quaternion.Euler(0, 90, 0);
                break;
            case 3: // Approaching Special Hidden Pit (X = 55)
                player.transform.position = new Vector3(50.0f, 0.5f, 50.0f);
                player.transform.rotation = Quaternion.Euler(0, 90, 0);
                break;
            case 4: // In Hidden Door chamber looking at the Cyan doorway (East wall X = 99)
                player.transform.position = new Vector3(90.0f, 0.5f, 50.0f);
                player.transform.rotation = Quaternion.Euler(0, 90, 0);
                break;
            case 5: // In Creature Room entrance (North) looking across the room toward West exit
                player.transform.position = new Vector3(98.0f, 0.5f, 20.0f);
                player.transform.rotation = Quaternion.Euler(0, -125, 0);
                break;
            case 7: // In Dark Fuse Room entrance looking toward central workbench with Green marker
                player.transform.position = new Vector3(45.0f, 0.5f, 11.0f);
                player.transform.rotation = Quaternion.Euler(0, -90, 0);
                break;
            case 8: // In Exit Elevator Room looking toward the Exit Elevator on West wall
                player.transform.position = new Vector3(5.0f, 0.5f, 11.0f);
                player.transform.rotation = Quaternion.Euler(0, -90, 0);
                break;
            default: // Back to start in Crash Room
                player.transform.position = new Vector3(-6.5f, 0.5f, 45.0f);
                player.transform.rotation = Quaternion.Euler(0, 50, 0);
                break;
        }

        if (cc != null) cc.enabled = true;
    }
}
