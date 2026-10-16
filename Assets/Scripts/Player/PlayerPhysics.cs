using UnityEngine;

public class PlayerPhysics : MonoBehaviour {
    public Rigidbody rb;
    public Transform cameraTransform;
    public QTMeshHandler planet;
    void FixedUpdate() {
        Vector3 dCenter = planet.planetCenter * planet.meshSize - transform.position;
        Vector3 gravUp = -dCenter.normalized;

        float worldRadius = planet.planetRadius * planet.meshSize;
        float m = worldRadius / (worldRadius + dCenter.magnitude - worldRadius);
        float gravMag = planet.surfaceGravity * m * m;

        Vector3 acceleration = dCenter.normalized * gravMag;

        rb.AddForce(acceleration * rb.mass);

        Vector3 camForwardOnPlane = Vector3.ProjectOnPlane(cameraTransform.forward, gravUp).normalized;

        if(camForwardOnPlane.sqrMagnitude > 0.001f) {
            Quaternion targetRotation = Quaternion.LookRotation(camForwardOnPlane, gravUp);
            transform.rotation = targetRotation;
        } else {
            Quaternion targetRotation = Quaternion.FromToRotation(transform.up, gravUp) * transform.rotation;
            transform.rotation = targetRotation;
        }
    }
}
