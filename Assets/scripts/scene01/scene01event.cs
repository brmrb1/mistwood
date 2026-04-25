using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class scene01event : MonoBehaviour
{
    public GameObject fadeScreenIn;
    public GameObject harlen;
    public GameObject seta;
    public GameObject textBox;
    // Start is called before the first frame update
    void Start()
    {
        StartCoroutine(EventStarter());
    }

    IEnumerator EventStarter()
    {
        yield return new WaitForSeconds(2);
        fadeScreenIn.SetActive(false);
        harlen.SetActive(true);
        yield return new WaitForSeconds(2);
        //文本功能
        textBox.SetActive(true);
        yield return new WaitForSeconds(2);
        seta.SetActive(true);

    }
}
