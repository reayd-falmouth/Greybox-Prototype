using UnityEngine;

public class DiceStop : MonoBehaviour
{
    private Rigidbody _rb;
    private bool _hasStopped;

    // Adjustable: How slow counts as "stopped"?
    public float stopSpeed = 0.1f; 

    void Start()
    {
        _rb = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        if (_hasStopped) return;

        // If the dice is barely moving across the floor...
        if (_rb.linearVelocity.magnitude < stopSpeed)
        {
            // ...but is still spinning...
            if (_rb.angularVelocity.magnitude > 0.1f)
            {
                // FORCE it to stop spinning by applying massive drag
                _rb.angularDamping = 50f; 
            }
        }
        else
        {
            // Reset drag for the next throw
            _rb.angularDamping = 0.05f; 
        }
        
        // Check if it has completely slept
        if (_rb.IsSleeping())
        {
            _hasStopped = true;
        }
    }
    
    // Reset the flag when thrown again
    public void OnThrown()
    {
        _hasStopped = false;
        _rb.angularDamping = 0.05f;
    }
}