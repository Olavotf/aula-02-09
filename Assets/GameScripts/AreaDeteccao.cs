using UnityEngine;

[RequireComponent(typeof(Collider))]
public class AreaDeteccao : MonoBehaviour
{
    [Tooltip("Tag que o inimigo deve detectar")]
    public string tagDoAlvo = "Player";

    [Tooltip("Referência ao script principal do inimigo")]
    public InimigoIA inimigo;

    private void Reset()
    {
        // Configura o collider automaticamente como trigger
        GetComponent<Collider>().isTrigger = true;

        // Tenta pegar o InimigoIA no pai
        if (inimigo == null)
            inimigo = GetComponentInParent<InimigoIA>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(tagDoAlvo))
        {
            if (inimigo != null)
                inimigo.DetectarAlvo(other.transform);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(tagDoAlvo))
        {
            if (inimigo != null)
                inimigo.PerderAlvo(other.transform);
        }
    }
}