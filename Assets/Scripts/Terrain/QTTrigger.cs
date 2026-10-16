using UnityEngine;

public class QTrigger : MonoBehaviour {
    public Transform player;
    public float minDstToUpdate;
    Vector3 playerPrevPos;
    public QTMeshHandler handler;
    void Update() {
        if(Vector3.Distance(playerPrevPos, player.position) >= minDstToUpdate) {
            handler.CreateQTChunks();
            playerPrevPos = player.position;
        }
    }
}
