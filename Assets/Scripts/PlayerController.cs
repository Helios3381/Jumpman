using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    private Rigidbody playerRb;

    public float jumpForce;
    public float gravityModifier;

    public bool isOnGround = true;

    private GameManager gameManager;
    public bool gameOver;

    private Animator playerAnimator;

    public ParticleSystem explosionParticle;
    public ParticleSystem dirtParticle;

    public AudioClip jumpSound;
    public AudioClip crashSound;

    private AudioSource playerAudio;

    //player action
    private bool playerJump;

    public enum PlayerAction
    {
        Start,
        Jumping,
        Dead
    }

    // Start is called before the first frame update
    void Start()
    {
        playerRb = GetComponent<Rigidbody>();
        Physics.gravity = new Vector3(0, -9.81f * gravityModifier, 0);
        playerAnimator = GetComponent<Animator>();
        playerAudio = GetComponent<AudioSource>();

        gameManager = GameObject.Find("GameManager").GetComponent<GameManager>();

        playerStatusUpdate(PlayerAction.Start);
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space) && isOnGround && !gameOver)
        {
            Debug.Log("Jump");
            playerJump = true;
        }
    }

    void FixedUpdate()
    {
        if (playerJump)
        {
            playerStatusUpdate(PlayerAction.Jumping);
            playerJump = false;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Obstacle"))
        {
            playerStatusUpdate(PlayerAction.Dead);
        }
    }

    void OnCollisionStay(Collision collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
        {
            dirtParticle.Play();
            isOnGround = true;
        }
    }

    void OnCollisionExit(Collision collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
        {
            isOnGround = false;
        }
    }

    private void playerStatusUpdate(PlayerAction action)
    {
        switch (action)
        {
            case PlayerAction.Start:
                gameOver = false;
                isOnGround = true;
                break;
            case PlayerAction.Jumping:
                dirtParticle.Stop();
                playerRb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
                isOnGround = false;
                playerAnimator.SetTrigger("Jump_trig");
                playerAudio.PlayOneShot(jumpSound, 1.0f);
                break;
            case PlayerAction.Dead:
                gameOver = true;
                dirtParticle.Stop();
                explosionParticle.Play();
                playerAnimator.SetBool("Death_b", true);
                playerAnimator.SetInteger("DeathType_int", 1);
                playerAudio.PlayOneShot(crashSound, 1.0f);
                Debug.Log("Game Over");
                gameManager.GameOver();
                gameManager.GetResult();
                break;
        }
    }

}
