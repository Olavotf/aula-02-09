using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerShoot : MonoBehaviour
{
    [Header("Shoot Settings")]
    [SerializeField] private GameObject bulletPrefab; // Arraste o Prefab da bala aqui
    [SerializeField] private Transform shootTransform; // Arraste o Transform de onde a bala sai
    [SerializeField] private float bulletVelocity = 20f; // Velocidade da bala

    private PlayerControls playerControls;

    private void Awake()
    {
        playerControls = new PlayerControls();
    }

    private void OnEnable()
    {
        playerControls.Enable();
        // Inscreve no evento de tiro (Shoot) que você criou no Input Actions
        playerControls.Movement.Shoot.performed += OnShootPerformed;
    }

    private void OnDisable()
    {
        // Desinscreve para evitar vazamento de memória
        playerControls.Movement.Shoot.performed -= OnShootPerformed;
        playerControls.Disable();
    }

    private void OnShootPerformed(InputAction.CallbackContext context)
    {
        if (bulletPrefab == null || shootTransform == null)
        {
            Debug.LogWarning("Bullet Prefab ou Shoot Transform não foram atribuídos no Inspetor!");
            return;
        }

        // Instancia a bala na posição e rotação do shootTransform
        GameObject bulletObj = Instantiate(bulletPrefab, shootTransform.position, shootTransform.rotation);

        // Pega o componente BulletController da bala instanciada
        BulletController bullet = bulletObj.GetComponent<BulletController>();

        if (bullet != null)
        {
            // Copia a direção "para frente" (eixo Z azul) do shootTransform
            bullet.direction = shootTransform.forward;
            // Define a velocidade
            bullet.velocity = bulletVelocity;
        }
    }
}