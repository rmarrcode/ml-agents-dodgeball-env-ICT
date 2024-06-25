using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;

[RequireComponent(typeof(AudioSource))]
public class ThrowBall : MonoBehaviour
{
    protected static readonly bool IS_DEBUG = false;
    public bool AllowKeyboardInput = true; //this mode ignores player input
    public bool initialized; //has this robot been initialized
    public KeyCode shootKey = KeyCode.J;
    [Header("AUTOSHOOT")] public bool autoShootEnabled;


    //SHOOTING RATE
    [Header("SHOOTING RATE")]
    public float shootingRate = .02f; //can shoot every shootingRate seconds. ex: .5 can shoot every .5 seconds

    public float coolDownTimer;
    public bool coolDownWait;

    //PROJECTILES
    [Header("PROJECTILE")] public GameObject projectilePrefab;
    public int numberOfProjectilesToPool = 25;
    public Transform projectileOrigin; //the transform the projectile will originate from
    public List<Rigidbody> projectilePoolList = new List<Rigidbody>(); //projectiles to shoot

    //FORCES
    [Header("FORCES")] public float forceToUse;

    [Header("MUZZLE FLASH")] public bool UseMuzzleFlash;
    public GameObject MuzzleFlashObject;

    [Header("SOUND")] public bool PlaySound;
    public ForceMode forceMode;
    protected AudioSource m_AudioSource;

    [Header("SCREEN SHAKE")] public bool UseScreenShake;

    [Header("TRANSFORM SHAKE")] public bool ShakeTransform;
    public float ShakeDuration = .1f;
    public float ShakeAmount = .1f;
    private Vector3 startPos;
    protected bool m_TransformIsShaking;

    public CinemachineImpulseSource impulseSource;

    // Start is called before the first frame update
    void Start()
    {
        if (!initialized)
        {
            Initialize();
        }
    }

    void OnEnable()
    {
        if (!initialized)
        {
            Initialize();
        }
    }

    void Initialize()
    {
        impulseSource = GetComponent<CinemachineImpulseSource>();
        projectilePoolList.Clear(); //clear list in case it's not empty
        for (var i = 0; i < numberOfProjectilesToPool; i++)
        {
            GameObject obj = Instantiate(projectilePrefab, transform.position, Quaternion.identity);
            Rigidbody p = obj.GetComponent<Rigidbody>();
            projectilePoolList.Add(p);
            p.transform.position = projectileOrigin.position;
            p.gameObject.SetActive(false);
        }

        if (MuzzleFlashObject)
        {
            MuzzleFlashObject.SetActive(false);
        }

        m_AudioSource = GetComponent<AudioSource>();

        initialized = true;
    }

    void FixedUpdate()
    {
        coolDownWait = coolDownTimer > shootingRate ? false : true;
        coolDownTimer += Time.fixedDeltaTime;
    }


    //ignoreTeam. 0 ignores team 0, 1 ignores team 1, -1 ignores no teams
    public void Throw(DodgeBall db, DodgeBallAgent thrower, int ignoreTeam = -1)
    {
        if (IS_DEBUG) Debug.Log("in throw function");
        if (coolDownWait || !gameObject.activeSelf)
        {
            return;
        }
        coolDownTimer = 0; //reset timer
        db.BallIsInPlay(true, ignoreTeam);
        db.thrownBy = thrower;
        FireProjectile(db.rb);
    }

    public void Drop(DodgeBall db)
    {
        var rb = db.rb;
        rb.transform.position = projectileOrigin.position;
        rb.transform.rotation = projectileOrigin.rotation;
        rb.gameObject.SetActive(true);
    }

    public void FireProjectile(Rigidbody rb)
    {
        rb.transform.position = projectileOrigin.position;
        rb.transform.rotation = projectileOrigin.rotation;

        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.gameObject.SetActive(true);
        rb.AddForce(projectileOrigin.forward * forceToUse, forceMode);
        if (UseScreenShake && impulseSource)
        {
            impulseSource.GenerateImpulse();
        }

        if (ShakeTransform && !m_TransformIsShaking)
        {
            StartCoroutine(I_ShakeTransform());
        }

        if (PlaySound)
        {
            m_AudioSource.PlayOneShot(m_AudioSource.clip);
        }
    }

    protected IEnumerator I_ShakeTransform()
    {
        m_TransformIsShaking = true;
        WaitForFixedUpdate wait = new WaitForFixedUpdate();

        if (UseMuzzleFlash && MuzzleFlashObject)
        {
            MuzzleFlashObject.transform.localScale = Random.Range(.5f, 1.5f) * Vector3.one;
            MuzzleFlashObject.SetActive(true);
        }

        float timer = 0;
        startPos = transform.localPosition;
        while (timer < ShakeDuration)
        {
            var pos = startPos + (Random.insideUnitSphere * ShakeAmount);
            transform.localPosition = pos;
            timer += Time.fixedDeltaTime;
            yield return wait;
        }

        transform.localPosition = startPos;
        if (UseMuzzleFlash && MuzzleFlashObject)
        {
            MuzzleFlashObject.SetActive(false);
        }

        m_TransformIsShaking = false;
    }

    //Autoshoot (only vertical)

    public void Throw(DodgeBall db, DodgeBallAgent thrower, int ignoreTeam = -1, float angle = 0)
    {
        if (IS_DEBUG) Debug.Log("in throw function");
        if (coolDownWait || !gameObject.activeSelf)
        {
            return;
        }
        coolDownTimer = 0; //reset timer
        db.BallIsInPlay(true, ignoreTeam);
        db.thrownBy = thrower;
        FireProjectile(db.rb, angle);
    }

    public void FireProjectile(Rigidbody rb, float angle)
    {
        rb.transform.position = projectileOrigin.position;
        rb.transform.rotation = projectileOrigin.rotation;

        //Modification Starts Here
        rb.transform.Rotate(angle * -90.0f + 45.0f, 0.0f, 0.0f, Space.Self);
        //Modification Ends Here 

        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.gameObject.SetActive(true);
        rb.AddForce(rb.transform.forward * forceToUse, forceMode);
        if (UseScreenShake && impulseSource)
        {
            impulseSource.GenerateImpulse();
        }

        if (ShakeTransform && !m_TransformIsShaking)
        {
            StartCoroutine(I_ShakeTransform());
        }

        if (PlaySound)
        {
            m_AudioSource.PlayOneShot(m_AudioSource.clip);
        }
    }

    //These versions allow for RL agents to deviate from automatic targetting to shoot ahead of moving targets

    public void Throw(DodgeBall db, DodgeBallAgent thrower, DodgeBallGameController.PlayerInfo target, float inputx, float inputy, int ignoreTeam = -1)
    {
        if (IS_DEBUG) Debug.Log("in throw function");
        if (coolDownWait || !gameObject.activeSelf)
        {
            return;
        }
        coolDownTimer = 0; //reset timer
        db.BallIsInPlay(true, ignoreTeam);
        db.thrownBy = thrower;
        FireProjectile(db.rb, target, inputx, inputy);
    }

    public void FireProjectile(Rigidbody rb, DodgeBallGameController.PlayerInfo target, float inputx, float inputy)
    {
        rb.transform.position = projectileOrigin.position;
        rb.transform.rotation = projectileOrigin.rotation;

        //Modification Starts Here
        if (target != null)
        {
            float dot = Vector3.Dot(rb.transform.forward, (rb.transform.position - target.Agent.gameObject.transform.position).normalized);
            if (dot > 0.8f || dot < -0.8f)
            {
                rb.transform.LookAt(target.Agent.gameObject.transform);
                rb.transform.Rotate(inputx * 5.0f, inputy * 5.0f, 0.0f, Space.Self);
            }

        }

        //Modification Ends Here 

        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.gameObject.SetActive(true);
        rb.AddForce(rb.transform.forward * forceToUse, forceMode);
        if (UseScreenShake && impulseSource)
        {
            impulseSource.GenerateImpulse();
        }

        if (ShakeTransform && !m_TransformIsShaking)
        {
            StartCoroutine(I_ShakeTransform());
        }

        if (PlaySound)
        {
            m_AudioSource.PlayOneShot(m_AudioSource.clip);
        }
    }

    //This version is fully automatic targetting 
    public void Throw(DodgeBall db, DodgeBallAgent thrower, DodgeBallGameController.PlayerInfo target, int ignoreTeam = -1)
    {
        if (IS_DEBUG) Debug.Log("in throw function");
        if (coolDownWait || !gameObject.activeSelf)
        {
            return;
        }
        coolDownTimer = 0; //reset timer
        db.BallIsInPlay(true, ignoreTeam);
        db.thrownBy = thrower;
        FireProjectile(db.rb, target);
    }

    public void FireProjectile(Rigidbody rb, DodgeBallGameController.PlayerInfo target)
    {
        rb.transform.position = projectileOrigin.position;
        rb.transform.rotation = projectileOrigin.rotation;

        //Modification Starts Here
        if (target != null)
        {
            float dot = Vector3.Dot(rb.transform.forward, (rb.transform.position - target.Agent.gameObject.transform.position).normalized);
            if (dot > 0.259f || dot < -0.259f) //75 degrees
            {
                
                rb.transform.LookAt(target.Agent.gameObject.transform);
            }
            
        }
        
        //Modification Ends Here 

        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.gameObject.SetActive(true);
        rb.AddForce(rb.transform.forward * forceToUse, forceMode);
        if (UseScreenShake && impulseSource)
        {
            impulseSource.GenerateImpulse();
        }

        if (ShakeTransform && !m_TransformIsShaking)
        {
            StartCoroutine(I_ShakeTransform());
        }

        if (PlaySound)
        {
            m_AudioSource.PlayOneShot(m_AudioSource.clip);
        }
    }

    //Fully automatic targetting for Waypoint-based agents
    public void Throw(DodgeBall db, DodgeBallAgent thrower, DodgeBallGameController_WP.PlayerInfo target, int ignoreTeam = -1)
    {
        if (IS_DEBUG) Debug.Log("in throw function");
        if (coolDownWait || !gameObject.activeSelf)
        {
            return;
        }
        coolDownTimer = 0; //reset timer
        db.BallIsInPlay(true, ignoreTeam);
        db.thrownBy = thrower;
        FireProjectile(db.rb, target);
    }

    public void FireProjectile(Rigidbody rb, DodgeBallGameController_WP.PlayerInfo target)
    {
        rb.transform.position = projectileOrigin.position;
        rb.transform.rotation = projectileOrigin.rotation;

        //Modification Starts Here
        if (target != null)
        {
            float dot = Vector3.Dot(rb.transform.forward, (rb.transform.position - target.Agent.gameObject.transform.position).normalized);
            if (dot > 0.8f || dot < -0.8f)
            {

                rb.transform.LookAt(target.Agent.gameObject.transform);
            }

        }

        //Modification Ends Here 

        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.gameObject.SetActive(true);
        rb.AddForce(rb.transform.forward * forceToUse, forceMode);
        if (UseScreenShake && impulseSource)
        {
            impulseSource.GenerateImpulse();
        }

        if (ShakeTransform && !m_TransformIsShaking)
        {
            StartCoroutine(I_ShakeTransform());
        }

        if (PlaySound)
        {
            m_AudioSource.PlayOneShot(m_AudioSource.clip);
        }
    }
}
