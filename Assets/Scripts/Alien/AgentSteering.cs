
using UnityEngine;
using UnityEngine.Rendering;

public class AgentSteering : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private float _speed = 5f;
    [SerializeField] private float _maxSteering = 5f;
    private Vector3 _velocity;
    void Start()
    {
        
    }

    // Update is called once per frame
    private void Update()
    {
        Seek();
        //Flee();
        Vector3 direction = target.position - transform.position;
        
        transform.position += direction.normalized *_speed * Time.deltaTime;
        transform.forward = _velocity;
    }

    private void Seek() // --> Persigue 
    {
        Vector3 desired = (target.position - transform.position).normalized;
        desired *= _speed;
        //Steering =  desiredVelocity - CurrentVelocity;
        Vector3 steering = desired - _velocity;
        //clamp -> Var A = MathF.Clamp (-5,0,10) -> limitar una variable entre 2 factores 
        steering = Vector3.ClampMagnitude(steering, _maxSteering * Time.deltaTime);

        _velocity += steering; 
    }

     /*private void Flee() //--> escapa
    {
        Vector3 desired = (transform.position - transform.position).normalized;
        desired *= _speed;
        //Steering =  desiredVelocity - CurrentVelocity;
        Vector3 steering = desired - _velocity;
        //clamp -> Var A = MathF.Clamp (-5,0,10) -> limitar una variable entre 2 factores 
        steering = Vector3.ClampMagnitude(steering, _maxSteering * Time.deltaTime);

        _velocity += steering; 
    }*/

}
