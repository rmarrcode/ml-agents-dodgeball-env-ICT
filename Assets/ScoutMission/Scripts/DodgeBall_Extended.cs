using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DodgeBall_Extended : DodgeBall
{
    [Header("Extended Properties")]
    private static readonly bool IS_DEBUG = false;
    public Vector3 spawnPosition;
    public bool destroyOnImpact = true;
    public Collider triggerCollider;
    private static float respawnTimer = 1f;
    public enum BallState
    {
        SPAWN,
        PICKUP,
        THROWABLE
    }
    public BallState ballState = BallState.SPAWN;

    private IEnumerator respawnCoroutine;

    //public DodgeBallAgent_Scout possessor = null;
    private int ignoreTeam;

    //private int unwantedCounter = 0, unwantedCounter_Limit = 100;
    private float unwantedTimer = 0f, unwantedTimer_Limit = 2f;

    [Header("Testing")]
    public bool test = false;
    public string tagString = "";

    private void FixedUpdate()
    {
        if (test)
        {
            RemoveDodgeball();
            test = false;
        }

        if ((ballState == BallState.THROWABLE) &&
            (transform.parent == null) &&
            (rb.velocity == Vector3.zero) &&
            (rb.angularVelocity == Vector3.zero)
            )
        {
            if (Time.timeSinceLevelLoad - unwantedTimer >= unwantedTimer_Limit)
            {
                CheckUnwantedState();
                unwantedTimer = Time.timeSinceLevelLoad;
            }
            /*unwantedCounter++;
            if (unwantedCounter >= unwantedCounter_Limit)
            {
                CheckUnwantedState();
                unwantedCounter = 0;
            }*/
        }
        else
        {
            //unwantedCounter = 0;
            unwantedTimer = Time.timeSinceLevelLoad;
        }
    }

    public virtual void SetPickupMode(BallState bs)
    {
        switch (bs)
        {
            case BallState.SPAWN:
                BallCollider.enabled = true;
                rb.detectCollisions = true;
                rb.useGravity = true;
                triggerCollider.enabled = false;
                break;
            case BallState.PICKUP:
                BallCollider.enabled = false;
                rb.detectCollisions = true;
                rb.useGravity = false;
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                triggerCollider.enabled = true;
                break;
            case BallState.THROWABLE:
                BallCollider.enabled = true;
                rb.detectCollisions = true;
                rb.useGravity = true;
                triggerCollider.enabled = false;
                break;
        }
    }


    public virtual void OnTriggerEnter(Collider col)
    {
        if (ballState == BallState.PICKUP)
        {
            if (IS_DEBUG) Debug.Log(thrownBy?.gameObject?.name + " throws. OnTriggerEnter Pickup. Different Agent? " + (col?.transform?.parent?.gameObject != thrownBy?.gameObject));
            if ((col != null) && ((thrownBy == null) || (col.gameObject != thrownBy.gameObject)))
            {
                if (col.gameObject.CompareTag("blueAgent") ||
                    col.gameObject.CompareTag("blueAgentFront") ||
                    col.gameObject.CompareTag("purpleAgent") ||
                    col.gameObject.CompareTag("purpleAgentFront")
                    )
                {
                    if (ballState == BallState.PICKUP)
                    {
                        if (IS_DEBUG) Debug.Log(gameObject.name + ": Trigger.Pickup. col=" + col.transform.parent.gameObject.name + ".");
                        col.transform.parent.gameObject.GetComponent<DodgeBallAgent_Scout>().CheckAndPickup(this);
                    }
                }
            }
        }
    }

    protected override void TagBallAs(string tag)
    {
        if (this == null) return;

        if (gameObject != null)
            gameObject.tag = tag;
        if (BallCollider != null && BallCollider.gameObject != null)
            BallCollider.gameObject.tag = tag;
    }

    protected override void OnCollisionEnter(Collision col)
    {
        tagString = col.gameObject.tag + " @ " + Time.realtimeSinceStartup;
        if (IS_DEBUG) Debug.Log("OnCollisionEnter: " + tagString);
        switch (ballState)
        {
            case BallState.SPAWN:
                if (col.gameObject.CompareTag("ground") ||
                    col.gameObject.CompareTag("wall") ||
                    col.gameObject.CompareTag("bush"))
                {
                    if (IS_DEBUG) Debug.Log(gameObject.name + " STATE: SPAWN0");
                    ballState = BallState.PICKUP;
                    SetPickupMode(ballState);
                    rb.velocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    transform.rotation = Quaternion.identity;
                }
                else if (col.gameObject.CompareTag("blueAgent") ||
                        col.gameObject.CompareTag("blueAgentFront") ||
                        col.gameObject.CompareTag("purpleAgent") ||
                        col.gameObject.CompareTag("purpleAgentFront"))
                {
                    if (IS_DEBUG) Debug.Log(gameObject.name + " STATE: SPAWN1");
                    ballState = BallState.THROWABLE;
                    SetPickupMode(ballState);
                }
                break;
            case BallState.PICKUP:
                if (col.gameObject.CompareTag("blueAgent") ||
                    col.gameObject.CompareTag("blueAgentFront") ||
                    col.gameObject.CompareTag("purpleAgent") ||
                    col.gameObject.CompareTag("purpleAgentFront"))
                {
                    if (IS_DEBUG) Debug.Log(gameObject.name + " STATE: PICKUP");
                    ballState = BallState.THROWABLE;
                    SetPickupMode(ballState);
                    TeamToIgnore = ignoreTeam;
                }
                break;
            case BallState.THROWABLE:
                if (destroyOnImpact)
                {
                    if (col.gameObject.CompareTag("ground")             ||
                        col.gameObject.CompareTag("wall")               ||
                        col.gameObject.CompareTag("bush")               ||
                        col.gameObject.CompareTag("blueAgent")          ||
                        col.gameObject.CompareTag("blueAgentFront")     ||
                        col.gameObject.CompareTag("purpleAgent")        ||
                        col.gameObject.CompareTag("purpleAgentFront")
                        )
                    {
                        if (IS_DEBUG) Debug.Log(gameObject.name + " STATE: THROWABLE");
                        if (IS_DEBUG) Debug.Log(gameObject + ". dodgeball_ext hit " + col.gameObject.name + "/" + col?.transform?.parent?.gameObject + "; thrownBy=" + (thrownBy?.gameObject?.name ?? "NULL") + "; inPlay=" + inPlay + " @ " + Time.realtimeSinceStartup);
                        // Projectile - CUBE(Clone)_11! (UnityEngine.GameObject). dodgeball_ext hit Ground/Large4v4Platform (UnityEngine.GameObject); thrownBy=DodgeballAgentBlue_Extended; inPlay=True @ 38.24921
                        //if (col?.transform?.parent?.gameObject != thrownBy?.gameObject)
                        if ((col?.gameObject != thrownBy?.gameObject) || (thrownBy == null))
                        {
                            RemoveDodgeball();
                        }
                    }
                }
                else
                {
                    base.OnCollisionEnter(col);
                }
                break;
        }

    }

    private void OnCollisionStay(Collision col)
    {
        if (ballState == BallState.THROWABLE)
        {
            if (col.gameObject.CompareTag("ground") ||
                col.gameObject.CompareTag("wall") ||
                col.gameObject.CompareTag("bush")
                )
            {
                CheckUnwantedState();
            }
        }
    }

    public virtual void RemoveDodgeball()
    {
        if (IS_DEBUG) Debug.Log(gameObject + ". " + thrownBy?.gameObject?.name + " throws. Remove Dodgeball. inPlay=" + inPlay + "; respawnTimer=" + respawnTimer);
        if (inPlay)
        {
            BallIsInPlay(false);
            //ignoreTeam = -1;
            //thrownBy = null;
            if (respawnTimer >= 0f)
            {
                StopTimedRespawn();
                respawnCoroutine = TimedRespawn();
                if (IS_DEBUG) Debug.Log(gameObject.name + " TimedRespawn start");
                StartCoroutine(respawnCoroutine);
            }
            if (respawnTimer != 0f)
            {
                SetActive(false);
            }
        }
        else
        {
            BallIsInPlay(false);
            thrownBy = null;
        }
    }

    IEnumerator TimedRespawn()
    {
        if (IS_DEBUG) Debug.Log(gameObject.name + " TimedRespawn(). inPlay=" + inPlay);
        int counter = 0;
        //BallIsInPlay(false);
        SetActive(false);
        while (!inPlay)
        {
            if ((counter >= 1) && (transform?.parent?.name == null))
            {
                yield return null;
                break;
            }
            if (IS_DEBUG) Debug.Log((counter++) + ": TimedRespawn coroutine for " + gameObject.name + "; parent=" + transform?.parent?.name);
            yield return new WaitForSeconds(respawnTimer);
        }
        ballState = BallState.SPAWN;
        thrownBy = null;
        SetPickupMode(ballState);
        transform.parent = null;
        transform.position = spawnPosition;//m_ResetPosition;
        SetActive(true);
    }

    public virtual void StopTimedRespawn()
    {
        if (respawnCoroutine != null)
        {
            StopCoroutine(respawnCoroutine);
        }
    }

    public virtual void SetActive(bool isActive)
    {
        if (IS_DEBUG) Debug.Log(gameObject?.name + " SetActive: " + isActive);
        BallCollider.enabled = isActive;
        BallCollider.gameObject.GetComponent<Renderer>().enabled = isActive;
        rb.detectCollisions = isActive;
        rb.useGravity = isActive;
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        transform.rotation = Quaternion.identity;
        if (isActive) gameObject.SetActive(true);
    }

    public virtual void SetThrowable(bool isActive = true)
    {
        transform.SetParent(null, true);
        BallCollider.enabled = isActive;
        BallCollider.gameObject.GetComponent<Renderer>().enabled = isActive;
        rb.detectCollisions = isActive;
        rb.useGravity = isActive;
        //rb.velocity = Vector3.zero;
        //rb.angularVelocity = Vector3.zero;
        transform.rotation = Quaternion.identity;
        if (isActive) gameObject.SetActive(true);

        //InvokeRepeating("CheckUnwantedState", 1f,1f );
    }

    public override void BallIsInPlay(bool p, int ignoreTeam = -1)
    {
        base.BallIsInPlay(p, ignoreTeam);
        if (ignoreTeam != -1)
        {
            this.ignoreTeam = ignoreTeam;
        }
    }

    public void CheckUnwantedState()
    {
        if ((ballState == BallState.THROWABLE) &&
            (transform.parent == null))
        {
            ballState = BallState.SPAWN;
            thrownBy = null;
            SetPickupMode(ballState);
            transform.parent = null;
            transform.position = spawnPosition;//m_ResetPosition;
            SetActive(true);
            //Debug.Log(gameObject + " found in unwanted state");
        }
    }
}
