using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MoveLeft : MonoBehaviour
{
    //start speed
    private float baseSpeed = 20f;
    //current game speed
    private float currentSpeed = 20f;
    //maximum speed
    private float maxSpeed = 40f;
    private float speedIncreaseRate = 0.2f;

    private GameManager gameManager;
    private PlayerController playerControllerScript;

    private float leftBound = -15;
    // Start is called before the first frame update
    void Start()
    {
        gameManager = GameObject.Find("GameManager").GetComponent<GameManager>();
        playerControllerScript = GameObject.Find("Player").GetComponent<PlayerController>();
    }

    // Update is called once per frame
    void Update()
    {
        if (!playerControllerScript.gameOver)
        {
            if (gameManager.isGameActive)
            {
                currentSpeed = baseSpeed + (gameManager.gameTime * speedIncreaseRate);
                currentSpeed = Mathf.Min(currentSpeed, maxSpeed);
                transform.Translate(Vector3.left * Time.deltaTime * currentSpeed);
            }
            else
            {
                currentSpeed = baseSpeed;
                transform.Translate(Vector3.left * Time.deltaTime * currentSpeed);
            }
        }

        if (transform.position.x < leftBound && gameObject.CompareTag("Obstacle"))
        {
            Destroy(gameObject);
        }
    }
}
