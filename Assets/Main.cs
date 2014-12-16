using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;

namespace Pool
{
	public class Main : MonoBehaviour 
	{
		enum State {Cue, Pool};

		/* Fig 1: 
		 * http://web.stanford.edu/group/billiards/PoolPhysicsSimulationByEventPrediction1MotionTransitions.pdf
		 */
		public class CueInfo
		{
			public float angle = 0;

			//x = a, y = b
			public Vector2 position = Vector2.zero;

			//21 ounces.
			public float mass = 0.59f;

			/* Not in fig, translation angle from ball reference to table reference, paralel to i-j plane. */
			public float forwardAngle = 0.0f;

			/* used for the camera */
			public float viewLength = 5.0f;

			public float cueVelocity = 1f;

			public CueInfo Clone()
			{
				return MemberwiseClone() as CueInfo;
			}

			public override string ToString ()
			{
				return string.Format("Angle: {0}, ForwardAngle: {1}, Position: {2}, mass: {3}", angle, forwardAngle, position, mass);
			}
		}

		State m_State;

		public GameObject m_PoolPlane;
		public List<GameObject> m_Balls = new List<GameObject>();
		public GameObject m_CueBall;
		public GameObject m_Cue;

		private float m_BallRadius = 1.0f;
		private Vector2 m_MousePosition;
		private Vector2 m_MouseMovement;
		private CueInfo m_CueInfo = new CueInfo();
		// Use this for initialization
		void Start () 
		{
			m_MousePosition = new Vector2(Input.mousePosition.x, Input.mousePosition.y);
			m_MouseMovement = Vector2.zero;
			m_State = State.Cue;
			m_PoolPlane = GameObject.Instantiate(Resources.Load("Table")) as GameObject;
			m_Cue = GameObject.Instantiate(Resources.Load("Cue")) as GameObject;
			m_CueBall = GameObject.Instantiate(Resources.Load("Ball")) as GameObject;
			m_CueBall.GetComponent<PhysicsModel>().precise = false;
			m_Balls.Add(m_CueBall);
			m_Cue.transform.position = m_Cue.transform.position + Vector3.up * 1;

			var ball = GameObject.Instantiate(Resources.Load("Ball")) as GameObject;
			//ball.GetComponent<PhysicsModel>().precise = true;
			ball.GetComponent<Colorize>().Color = Color.grey;

			m_Balls.Add(ball);

			m_BallRadius = 0.056f;

			m_PoolPlane.transform.position += Vector3.down * m_BallRadius;
		}
		
		// Update is called once per frame
		void Update () 
		{
			m_MouseMovement = new Vector2(Input.mousePosition.x, Input.mousePosition.y) - m_MousePosition;
			m_MousePosition = new Vector2(Input.mousePosition.x, Input.mousePosition.y);
			//Debug.Log(m_MousePosition + " " + m_MouseMovement);
			if (m_State == State.Cue)
			{
				m_CueBall.GetComponent<Transform>().position = Vector3.zero;
				m_Balls[1].GetComponent<Transform>().position = Vector3.zero;
				DoCue();
			}
			else if (m_State == State.Pool)
			{
				DoPool();
			}
		}

		void DoCue()
		{
			var tCue = m_Cue.GetComponent<Transform>();
			var tBall = m_CueBall.GetComponent<Transform>();

			if (Input.mousePresent && Input.GetMouseButtonDown(0))
			{
				m_CueBall.GetComponent<PhysicsModel>().Strike(m_CueInfo);
				m_Balls[1].GetComponent<PhysicsModel>().Strike(m_CueInfo);

				m_CueInfo = m_CueInfo.Clone();
			
				m_State = State.Pool;
			}
			else if (Input.mousePresent && Input.GetMouseButton(1))
			{
				Vector2 newPos = m_CueInfo.position + (m_MouseMovement * Time.deltaTime * 0.1f);
				if (Mathf.Pow(newPos.x, 2.0f) + Mathf.Pow(newPos.y, 2.0f) < Mathf.Pow(m_BallRadius, 2.0f))
				{
					m_CueInfo.position = newPos;
				}
			}
			else if (Input.mousePresent)
			{
				m_CueInfo.angle = Mathf.Clamp(Mathf.LerpAngle(m_CueInfo.angle, m_CueInfo.angle + m_MouseMovement.y, Time.deltaTime / 10), 0.01f, Mathf.PI / 4);

				m_CueInfo.forwardAngle = Mathf.LerpAngle(m_CueInfo.forwardAngle, m_CueInfo.forwardAngle - m_MouseMovement.x, Time.deltaTime / 1);
			}
			m_CueInfo.viewLength = Mathf.Clamp(m_CueInfo.viewLength - Input.GetAxis("Mouse ScrollWheel"), 0, 10);

			var cuePos = tCue.position;
			cuePos = tBall.position + new Vector3(Mathf.Cos(m_CueInfo.forwardAngle) * (2 * m_BallRadius), 0, Mathf.Sin( m_CueInfo.forwardAngle) * (2 * m_BallRadius));

			cuePos += new Vector3(0, m_CueInfo.position.y, 0);
			cuePos += new Vector3(Mathf.Cos(m_CueInfo.forwardAngle + Mathf.PI / 2) * m_CueInfo.position.x, 0, Mathf.Sin(m_CueInfo.forwardAngle + Mathf.PI / 2) * m_CueInfo.position.x);

			tCue.position = cuePos;

			var direction = (tCue.position - tBall.position).normalized;
			var q = Quaternion.Euler(0, -m_CueInfo.forwardAngle * Mathf.Rad2Deg, m_CueInfo.angle * Mathf.Rad2Deg);
			tCue.rotation = q;

			Camera c = GetComponent<Camera>();
			transform.position = tBall.position + new Vector3(Mathf.Cos(m_CueInfo.forwardAngle) * m_CueInfo.viewLength, 0, Mathf.Sin( m_CueInfo.forwardAngle) * m_CueInfo.viewLength);
			transform.position = transform.position + Vector3.up * ((m_BallRadius + tCue.localScale.z + m_CueInfo.viewLength / 2) / 2);
			transform.LookAt(tBall.position);
		}

		void DoPool()
		{
			foreach(var ball in m_Balls)
			{

			}
			bool someBallIsMoving = false;
			foreach(var ball in m_Balls)
			{
				if (!ball.GetComponent<PhysicsModel>().Still)
				{
					someBallIsMoving = true;
				}
			}
			if (!someBallIsMoving)
			{
				m_State = State.Cue;
			}
		}
	}
}