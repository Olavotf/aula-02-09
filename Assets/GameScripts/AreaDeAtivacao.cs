using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Collider))]
public class AreaDeAtivacao : MonoBehaviour
{
    [Tooltip("Tag do alvo que ativa os inimigos")]
    public string tagDoAlvo = "Player";

    [Tooltip("Inimigos que serão ativados quando o player entrar nesta área")]
    public List<InimigoArea> inimigosNaArea = new List<InimigoArea>();

    [Tooltip("Se verdadeiro, os inimigos voltam a patrulhar quando o player sair")]
    public bool desativarAoSair = true; // <- renomeie para desativarAoSair

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(tagDoAlvo)) return;

        foreach (var inimigo in inimigosNaArea)
        {
            if (inimigo != null)
                inimigo.AtivarPerseguicao(other.transform);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(tagDoAlvo)) return;
        if (!desativarAoSair) return;

        foreach (var inimigo in inimigosNaArea)
        {
            if (inimigo != null)
                inimigo.DesativarPerseguicao(other.transform);
        }
    }

    // Desenha a área no editor para facilitar a visualização
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.25f);
        Collider col = GetComponent<Collider>();
        if (col != null)
            Gizmos.DrawCube(col.bounds.center, col.bounds.size);
    }
}