

using Mirror;

public class GameController : NetworkBehaviour
{
    public static GameController Instance { get; private set; }

    public Projectile[] ProjectilePrefabs;

    private void Awake()
    {
        Instance = this;
    }

    public override void OnStartClient()
    {
        Projectile.Init();
    }
}