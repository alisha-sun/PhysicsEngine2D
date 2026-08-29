using System;
using Unity.Netcode;
using UnityEngine;
public class PlayerController : NetworkBehaviour
{
    [Header("Movement Settings")]
    public float movementSpeed;
    [Header("Jump Settings")]
    public float jumpForce;
    public float jumpCooldown;
    public int maxAirJumps;
    [Header("Dash Settings")]
    public float dashSpeed;
    public float dashCooldown;
    public float dashDuration;
    public float diveSpeed;

    Rigidbody2D rb;

    private float timeSinceLastJump = 0f;
    private float timeSinceLastDash = 0f;

    private int airJumpCount = 0;
    private bool usedAirDash = false;

    private int dashDirection;
    private float gravityScale;
    private bool isGrounded;
    private bool inDive = false;
    private bool swappedControls = false;
    private bool canSwap_this = true;
    private bool canSwap_other = true;

    public PlayerControls pc_this = new PlayerControls(); 
    public PlayerControls pc_other = new PlayerControls();

    private void Start()
    {
        if (NetworkManager.Singleton.IsServer)
        {
            GetComponent<NetworkObject>().Spawn();
        }
    }
    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        gravityScale = rb.gravityScale;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Ground"))
        {
            isGrounded = true;
            airJumpCount = 0;
            usedAirDash = false;
            timeSinceLastJump = jumpCooldown; //cooldown only applies to air jumps
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Ground")) isGrounded = false;
    }

    void Update()
    {   
        timeSinceLastJump = Math.Min(jumpCooldown, timeSinceLastJump + Time.deltaTime);
        timeSinceLastDash = Math.Min(dashCooldown, timeSinceLastDash + Time.deltaTime);

        if (Input.GetKeyDown(KeyCode.W)) pc_this.W.pressed = true;
        if (Input.GetKeyDown(KeyCode.A)) pc_this.A.pressed = true;
        if (Input.GetKeyDown(KeyCode.S)) pc_this.S.pressed = true;
        if (Input.GetKeyDown(KeyCode.D)) pc_this.D.pressed = true;
        if (Input.GetKeyDown(KeyCode.Space)) pc_this.Space.pressed = true;
        if (Input.GetKeyDown(KeyCode.Alpha0)) pc_this.Alpha0.pressed = true;

        pc_this.W.held = Input.GetKey(KeyCode.W);
        pc_this.A.held = Input.GetKey(KeyCode.A);
        pc_this.S.held = Input.GetKey(KeyCode.S);
        pc_this.D.held = Input.GetKey(KeyCode.D);
        pc_this.Space.held = Input.GetKey(KeyCode.Space);
        pc_this.Alpha0.held = Input.GetKey(KeyCode.Alpha0);
    }

    private void FixedUpdate()
    {
        if (!IsServer)
        {
            SendInputServerRpc(pc_this);
            pc_this.Reset();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void SendInputServerRpc(PlayerControls pc)
    {
        pc_other = pc;
        if (!swappedControls) { //default controls
            Move(pc_this, pc_other);
        }
        else {
            Move(pc_other, pc_this);
        }

        if (canSwap_this && canSwap_other && pc_this.Alpha0.held && pc_other.Alpha0.held)
        {
            swappedControls = !swappedControls;
            canSwap_this = false;
            canSwap_other = false;
        }
        else {
            if (!canSwap_this && !pc_this.Alpha0.held) canSwap_this = true;
            if (!canSwap_other && !pc_other.Alpha0.held) canSwap_other = true;
        }

        if (timeSinceLastDash >= dashDuration)
        {
            rb.gravityScale = gravityScale;
        }

        pc_this.Reset();
    }

    private void Move(PlayerControls movementInput, PlayerControls abilityInput){ //Priority: jump > dive >= dash
        Vector2 velocity = rb.velocity;
        velocity.x = 0f;
        bool jumped = false;

        if (timeSinceLastJump >= jumpCooldown)
        { 
            if (isGrounded && abilityInput.W.pressed)
            { //jump
                timeSinceLastJump = 0f;
                velocity.y = jumpForce;
                jumped = true;
                inDive = false;
                timeSinceLastDash = dashDuration;
            }
            else if (!isGrounded && airJumpCount < maxAirJumps && ((airJumpCount % 2 == 0 && movementInput.W.pressed) || (airJumpCount % 2 == 1 && abilityInput.W.pressed))) //alternating controls for air jumping (for now there's only double and triple but I might change that later)
            {
                timeSinceLastJump = 0f;
                airJumpCount++;
                velocity.y = jumpForce;
                jumped = true;
                inDive = false;
                timeSinceLastDash = dashDuration;
            } //jumping overrides diving and dashing
        }

        if (timeSinceLastDash >= dashDuration && !jumped && movementInput.S.held) //Crouching or diving cannot interrupt a dash. You cannot start crouching or diving in the same frame that you jump
        {
            if (abilityInput.S.pressed)
            {
                inDive = true;
                velocity.x = 0f;
            }
            else if (isGrounded) { }//crouch
        }

        if (inDive)
        {
            if (isGrounded)
            {
                inDive = false;
                //do damage
            }
            else if (abilityInput.S.held) velocity.y = -diveSpeed;
            else inDive = false;
        }
        else { //!inDive; you cannot move horizontally while diving
            if (timeSinceLastDash < dashDuration)
            { //currently in dash
                if (dashDirection == 1) velocity.x = Math.Max(velocity.x, dashDirection * dashSpeed);
                else velocity.x = Math.Min(velocity.x, dashDirection * dashSpeed);
            }
            else if (movementInput.A.held && !movementInput.D.held && (!abilityInput.D.held || abilityInput.A.held)) //the abilities player can't stop movement in both directions by pressing both A and D
            { //moving left
                velocity.x = Math.Min(velocity.x, -movementSpeed);
                if (!usedAirDash && !jumped && timeSinceLastDash >= dashCooldown && abilityInput.A.pressed && !abilityInput.D.pressed) //you cannot start a dash in the same frame that you jump
                { //dash left
                    dashDirection = -1;
                    timeSinceLastDash = 0f;
                    velocity.y = 0f;
                    velocity.x = Math.Min(velocity.x, dashDirection * dashSpeed);
                    rb.gravityScale = 0f;
                    if (!isGrounded) usedAirDash = true;
                }
            }
            else if (movementInput.D.held && !movementInput.A.held && (!abilityInput.A.held || abilityInput.D.held))
            { //moving right
                velocity.x = Math.Max(velocity.x, movementSpeed);
                if (!usedAirDash && !jumped && timeSinceLastDash >= dashCooldown && abilityInput.D.pressed && !abilityInput.A.pressed) 
                { //dash right
                    dashDirection = 1;
                    timeSinceLastDash = 0f;
                    velocity.y = 0f;
                    velocity.x = Math.Max(velocity.x, dashDirection * dashSpeed);
                    rb.gravityScale = 0f;
                    if (!isGrounded) usedAirDash = true;
                }
            }
        }

        rb.velocity = velocity;
    }

    public struct PlayerControls : INetworkSerializable {
        public KeyState W;
        public KeyState A;
        public KeyState S;
        public KeyState D;
        public KeyState Space;
        public KeyState Alpha0;
        public void Reset() {
            this.W.pressed = false;
            this.A.pressed = false;
            this.S.pressed = false;
            this.D.pressed = false;
            this.Space.pressed = false;
            this.Alpha0.pressed = false;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter {
            serializer.SerializeValue(ref W);
            serializer.SerializeValue(ref A);
            serializer.SerializeValue(ref S);
            serializer.SerializeValue(ref D);
            serializer.SerializeValue(ref Space);
            serializer.SerializeValue(ref Alpha0);
        }
    }
    public struct KeyState : INetworkSerializable {
        public bool pressed;
        public bool held;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter {
            serializer.SerializeValue(ref pressed);
            serializer.SerializeValue(ref held);
        }
    }
}