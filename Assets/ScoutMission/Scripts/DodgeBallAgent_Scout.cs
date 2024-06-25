using System;
using System.Collections;
using System.Collections.Generic;
using MLAgents;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Policies;
using Random = UnityEngine.Random;

//DodgeBallAgent_Scout adds functionality for infinite ammo 

public class DodgeBallAgent_Scout : DodgeBallAgent
{
    [Header("Extended Parameters")]
    public bool useInfiniteAmmo = true;
    public int maxAmmoCapacity = 4;
    public bool destroyBallsOnImpact = true;

    public virtual void SetAmmoCapacity(int newCapacity)
    {
        if (BallUIList.Count > newCapacity)
            BallUIList.RemoveRange(newCapacity, BallUIList.Count - newCapacity);
    }

    public override void ResetAgent()
    {
        //base.ResetAgent();

        //Reset Position
        GetAllParameters();
        StopAllCoroutines();
        transform.position = m_StartingPos;
        AgentRb.constraints = RigidbodyConstraints.FreezeRotation;
        AgentRb.velocity = Vector3.zero;
        AgentRb.angularVelocity = Vector3.zero;

        //Randomly rotate agent during training
        if (m_GameController.CurrentSceneType == DodgeBallGameController.SceneType.Game || m_GameController.CurrentSceneType == DodgeBallGameController.SceneType.Movie)
        {
            transform.rotation = m_StartingRot;
        }
        else //Training Mode 
        {
            transform.rotation = Quaternion.Euler(new Vector3(0f, Random.Range(0, 360)));
        }

        //Reset dodgeballs 
        if (!useInfiniteAmmo)
            ActiveBallsQueue.Clear();
        currentNumberOfBalls = 0;
        if (!useInfiniteAmmo)
            SetActiveBalls(0);

        //Reset parameters 
        NormalEyes.gameObject.SetActive(true);
        HitEyes.gameObject.SetActive(false);
        HasEnemyFlag = false;
        Stunned = false;
        AgentRb.drag = 4;
        AgentRb.angularDrag = 1;
        Dancing = false;

        //Setup infinite projectiles
        if (useInfiniteAmmo)
        {
            base.SetActiveBalls(maxAmmoCapacity);
            PickUpBall(currentlyHeldBalls[0]);
            currentNumberOfBalls = maxAmmoCapacity;
        }
        else
        {
            foreach (DodgeBall d in currentlyHeldBalls)
            {
                Destroy(d.gameObject);
            }
            currentlyHeldBalls.Clear();
        }
        SetAmmoCapacity(maxAmmoCapacity);
    }


    public override void ThrowTheBall()
    {
        if ((currentNumberOfBalls > 0) && !ThrowController.coolDownWait) 
        {
            if (IS_DEBUG) Debug.Log("A) " + gameObject.name + " throws the ball! #b=" + currentNumberOfBalls);
            var db = ActiveBallsQueue.Peek();
            if (db != null && db.GetComponent<Rigidbody>() != null)
            {
                //((DodgeBall_Extended)db).SetActive(true);
                ThrowController.Throw(db, this, m_BehaviorParameters.TeamId);
                StartCoroutine(_ActivateAfterThrow());
                IEnumerator _ActivateAfterThrow()
                {
                    yield return new WaitForEndOfFrame();
                    ((DodgeBall_Extended)db).SetThrowable(true);
                }
            }
            else
            {
                if (IS_DEBUG) Debug.Log("Tried throwing null ball");
            }
            if (!useInfiniteAmmo)
            {
                ActiveBallsQueue.Dequeue();
                currentNumberOfBalls--;
            }
            SetActiveBalls(currentNumberOfBalls);
            if (IS_DEBUG) Debug.Log("B) " + gameObject.name + " throws the ball! #b=" + currentNumberOfBalls);
        }
    }

    protected override void PickUpBall(DodgeBall db)
    {
        if (m_GameController.ShouldPlayEffects && (m_GameController.BallPickupClip != null))
        {
            m_BallImpactAudioSource.PlayOneShot(m_GameController.BallPickupClip, .1f);
        }

        //update counter
        currentNumberOfBalls++;
        SetActiveBalls(currentNumberOfBalls);
        if (IS_DEBUG) Debug.Log(gameObject?.name + " throws. Pickup Ball. #balls=" + currentNumberOfBalls);

        if (!useInfiniteAmmo || (useInfiniteAmmo && (currentNumberOfBalls < maxAmmoCapacity)))
        {
            //add to our inventory
            ActiveBallsQueue.Enqueue(db);
            db.BallIsInPlay(true);
            //db.gameObject.SetActive(false);
            //((DodgeBall_Extended)db).SetActive(false);
            db.transform.parent = transform;
            db.transform.localPosition = Vector3.zero;
        }
    }

    public override void DropAllBalls()
    {
        if (!useInfiniteAmmo)
        {
            base.DropAllBalls();
        }
    }


    protected override void OnCollisionEnter(Collision col)
    {
        // Ignore all collisions when stunned
        if (Stunned)
        {
            return;
        }
        DodgeBall db = col.gameObject.GetComponent<DodgeBall>();
        if (!db)
        {
            if (m_GameController.GameMode == DodgeBallGameController.GameModeType.CaptureTheFlag)
            {
                // Check if it is a flag
                if (col.gameObject.tag == "purpleFlag" && teamID == 0 || col.gameObject.tag == "blueFlag" && teamID == 1)
                {
                    m_GameController.FlagWasTaken(this);
                }
                else if (col.gameObject.tag == "purpleFlag" && teamID == 1 || col.gameObject.tag == "blueFlag" && teamID == 0)
                {
                    m_GameController.ReturnFlag(this);
                }
                DodgeBallAgent hitAgent = col.gameObject.GetComponent<DodgeBallAgent>();
                if (hitAgent && HasEnemyFlag && m_GameController.FlagCarrierKnockback)
                {
                    if (hitAgent.teamID != teamID && !hitAgent.Stunned)
                    {
                        if (m_GameController.ShouldPlayEffects && (m_GameController.FlagHitClip != null))
                        {
                            m_BallImpactAudioSource.PlayOneShot(m_GameController.FlagHitClip, 1f);
                        }
                        // Play Flag Whack
                        if (FlagAnimator != null)
                        {
                            FlagAnimator.SetTrigger("FlagSwing");
                        }
                        hitAgent.PlayHitFX();
                        var moveDirection = hitAgent.transform.position - transform.position;
                        hitAgent.AgentRb.AddForce(moveDirection * 150, ForceMode.Impulse);
                    }
                }
            }
            return;
        }

        if (db != null) { if (IS_DEBUG) Debug.Log("A) " + gameObject.name + " hit by " + col.gameObject.name + "; inPlay? " + db.inPlay + "; state=" + ((DodgeBall_Extended)db).ballState); }
        //if (db.inPlay) //HIT BY LIVE BALL
        if (((DodgeBall_Extended)db).ballState == DodgeBall_Extended.BallState.THROWABLE)
        {
            if (IS_DEBUG) Debug.Log("B) hit by: teamToIgnore=" + db.TeamToIgnore + "; teamID=" + m_BehaviorParameters.TeamId + "; thrownby teamID=" + db?.thrownBy?.teamID);
            //if (db.TeamToIgnore != -1 && db.TeamToIgnore != m_BehaviorParameters.TeamId) //HIT BY LIVE BALL
            if ((db?.thrownBy != null) && (db?.thrownBy?.teamID != m_BehaviorParameters.TeamId))
            {
                PlayHitFX();
                m_GameController.PlayerWasHit(this, db.thrownBy);
                if (IS_DEBUG) Debug.Log("C) " + gameObject + " hit by " + col.gameObject.name + " @ " + Time.realtimeSinceStartup);
                if (db.gameObject.GetComponent<DodgeBall_Extended>())
                {
                    if (db.gameObject.GetComponent<DodgeBall_Extended>().destroyOnImpact)
                        db.gameObject.GetComponent<DodgeBall_Extended>().RemoveDodgeball();
                    else
                        db.BallIsInPlay(false);

                }
                else
                {
                    db.BallIsInPlay(false);
                }
            }
        }
        else //TRY TO PICK IT UP
        {
            if (currentNumberOfBalls < maxAmmoCapacity)
            {
                PickUpBall(db);
            }
        }
    }

    public virtual void CheckAndPickup(DodgeBall_Extended db)
    {
        if (IS_DEBUG) Debug.Log(gameObject.name + ": CheckAndPickup: " + currentNumberOfBalls);
        if (currentNumberOfBalls < maxAmmoCapacity)
        {
            db.ballState = DodgeBall_Extended.BallState.THROWABLE;
            db.SetPickupMode(db.ballState);
            PickUpBall(db);
        }
    }

}
