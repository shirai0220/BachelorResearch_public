using UnityEngine;

using UnityEngine.XR;
using UnityEngine.SceneManagement;

public class StartController : MonoBehaviour
{
    void Start()
    {
        SceneManager.LoadScene("tutorial_eye_movement_previous_research_three_table");
    }
}