using UnityEngine;

namespace BalloonSimulation.JobSystem
{
    /// <summary>
    /// Free look camera controller for balloon simulation
    /// Provides smooth camera movement and rotation controls
    /// </summary>
    public class BalloonFreeLookCamera : MonoBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private float moveSpeed = 20f;
        [SerializeField] private float fastMoveSpeed = 50f;
        [SerializeField] private float verticalSpeed = 15f;
        [SerializeField] private float acceleration = 5f;
        [SerializeField] private float damping = 5f;
        
        [Header("Look Settings")]
        [SerializeField] private float lookSensitivity = 2f;
        [SerializeField] private float smoothTime = 0.1f;
        [SerializeField] private bool invertY = false;
        [SerializeField] private float minPitch = -80f;
        [SerializeField] private float maxPitch = 80f;
        
        [Header("Input Settings")]
        [SerializeField] private KeyCode fastMoveKey = KeyCode.LeftShift;
        [SerializeField] private int mouseButton = 1; // Right mouse button
        [SerializeField] private bool requireMouseButton = true;
        
        [Header("Bounds")]
        [SerializeField] private bool constrainToBounds = true;
        [SerializeField] private Bounds movementBounds = new Bounds(Vector3.zero, Vector3.one * 200f);
        
        // Internal state
        private Vector3 velocity;
        private float rotationX;
        private float rotationY;
        private Vector3 smoothRotation;
        private Vector3 currentRotation;
        
        // Input state
        private bool isLooking;
        private Vector3 lastMousePosition;
        
        private void Start()
        {
            // Initialize rotation from current transform
            Vector3 euler = transform.eulerAngles;
            rotationX = euler.y;
            rotationY = euler.x;
            currentRotation = euler;
            
            // Lock cursor if needed
            if (!requireMouseButton)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
        
        private void Update()
        {
            HandleInput();
            HandleMovement();
            HandleRotation();
        }
        
        private void HandleInput()
        {
            // Check if we should be looking around
            if (requireMouseButton)
            {
                isLooking = Input.GetMouseButton(mouseButton);
                
                if (isLooking && Input.GetMouseButtonDown(mouseButton))
                {
                    lastMousePosition = Input.mousePosition;
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
                else if (!isLooking && Input.GetMouseButtonUp(mouseButton))
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
            }
            else
            {
                isLooking = true;
            }
        }
        
        private void HandleMovement()
        {
            // Get input axes
            float horizontal = Input.GetAxis("Horizontal");
            float vertical = Input.GetAxis("Vertical");
            float upDown = 0f;
            
            // Vertical movement
            if (Input.GetKey(KeyCode.Q))
                upDown = -1f;
            else if (Input.GetKey(KeyCode.E))
                upDown = 1f;
            
            // Calculate movement direction
            Vector3 moveDirection = Vector3.zero;
            
            if (Mathf.Abs(horizontal) > 0.01f || Mathf.Abs(vertical) > 0.01f || Mathf.Abs(upDown) > 0.01f)
            {
                // Get camera forward and right vectors
                Vector3 forward = transform.forward;
                Vector3 right = transform.right;
                
                // Remove Y component for horizontal movement
                forward.y = 0f;
                forward.Normalize();
                right.y = 0f;
                right.Normalize();
                
                // Calculate movement
                moveDirection = (forward * vertical + right * horizontal).normalized;
                moveDirection += Vector3.up * upDown;
                
                // Apply speed
                float currentSpeed = Input.GetKey(fastMoveKey) ? fastMoveSpeed : moveSpeed;
                moveDirection *= currentSpeed;
            }
            
            // Apply acceleration and damping
            velocity = Vector3.Lerp(velocity, moveDirection, acceleration * Time.deltaTime);
            
            // Apply movement
            Vector3 newPosition = transform.position + velocity * Time.deltaTime;
            
            // Constrain to bounds if enabled
            if (constrainToBounds)
            {
                newPosition = new Vector3(
                    Mathf.Clamp(newPosition.x, movementBounds.min.x, movementBounds.max.x),
                    Mathf.Clamp(newPosition.y, movementBounds.min.y, movementBounds.max.y),
                    Mathf.Clamp(newPosition.z, movementBounds.min.z, movementBounds.max.z)
                );
            }
            
            transform.position = newPosition;
            
            // Apply damping when no input
            if (moveDirection.magnitude < 0.01f)
            {
                velocity = Vector3.Lerp(velocity, Vector3.zero, damping * Time.deltaTime);
            }
        }
        
        private void HandleRotation()
        {
            if (!isLooking) return;
            
            // Get mouse delta
            float mouseX = Input.GetAxis("Mouse X");
            float mouseY = Input.GetAxis("Mouse Y");
            
            // Apply sensitivity
            mouseX *= lookSensitivity;
            mouseY *= lookSensitivity;
            
            // Apply inversion
            if (invertY)
                mouseY = -mouseY;
            
            // Update rotation
            rotationX += mouseX;
            rotationY -= mouseY;
            
            // Clamp pitch
            rotationY = Mathf.Clamp(rotationY, minPitch, maxPitch);
            
            // Smooth rotation
            currentRotation = Vector3.SmoothDamp(
                currentRotation,
                new Vector3(rotationY, rotationX, 0f),
                ref smoothRotation,
                smoothTime
            );
            
            // Apply rotation
            transform.rotation = Quaternion.Euler(currentRotation);
        }
        
        /// <summary>
        /// Set camera position and look at target
        /// </summary>
        public void SetPositionAndLookAt(Vector3 position, Vector3 target)
        {
            transform.position = position;
            transform.LookAt(target);
            
            // Update internal rotation state
            Vector3 euler = transform.eulerAngles;
            rotationX = euler.y;
            rotationY = euler.x;
            currentRotation = euler;
        }
        
        /// <summary>
        /// Set movement bounds
        /// </summary>
        public void SetMovementBounds(Bounds bounds)
        {
            movementBounds = bounds;
            constrainToBounds = true;
        }
        
        /// <summary>
        /// Reset camera to default position
        /// </summary>
        public void ResetCamera()
        {
            transform.position = new Vector3(0, movementBounds.size.y * 0.3f, -movementBounds.size.z * 0.5f);
            transform.rotation = Quaternion.Euler(15f, 0f, 0f);
            
            velocity = Vector3.zero;
            rotationX = 0f;
            rotationY = 15f;
            currentRotation = new Vector3(15f, 0f, 0f);
        }
        
        private void OnDrawGizmosSelected()
        {
            if (constrainToBounds)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireCube(movementBounds.center, movementBounds.size);
            }
        }
        
        private void OnDisable()
        {
            // Reset cursor state
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}