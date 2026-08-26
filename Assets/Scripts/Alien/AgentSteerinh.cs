using UnityEngine;
using UnityEngine.Rendering;

public class Alien : MonoBehaviour
{
    [SerializeField] private Transform target;

    private Vector3 _velocity;
    void Start()
    {
        
    }

    // Update is called once per frame
    private void Update()
    {
        Vector3 direction = target.position - transform.position;
        transform.position = direction.normalized * Time.deltaTime;
    }

    private void seek()
    {
        Vector3 desired = target.position - transform.position;
        //Steering =  desiredVelocity - CurrentVelocity;
        Vector3 steering = desired - _velocity;

        _velocity = steering;
    }
}
