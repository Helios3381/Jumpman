using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ButtonEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private Button button;
    private GameManager gameManager;
    //sound effects
    public AudioSource buttonAudio;
    public AudioClip buttonSound;

    // Start is called before the first frame update
    void Start()
    {
        buttonAudio = GetComponent<AudioSource>();
        button = GetComponent<Button>();
        gameManager = GameObject.Find("GameManager").GetComponent<GameManager>();
        Debug.Log("Start");
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        Debug.Log("Mouse entered the button: " + gameObject.name);
        buttonAudio.PlayOneShot(buttonSound, 1.0f);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        Debug.Log("Mouse exited the button: " + gameObject.name);
    }
}
