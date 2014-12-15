using UnityEngine;
using System.Collections;

namespace Pool
{
	/* Physics from:
	 * 	http://web.stanford.edu/group/billiards/PoolPhysicsSimulationByEventPrediction1MotionTransitions.pdf
	 */
	public class PhysicsModel : MonoBehaviour 
	{
		public bool Still {get; private set;}
		public float Radius {get; private set;}
		
		Vector3 AngularVelocity { get { return CalculateAngularVelocity(m_Time); } }
		Vector3 Velocity {get { return CalculateVelocity(m_Time); } }

		private float m_ForwardAngle;
		private float m_Mass = 0.15f;
		private float m_Radius;
		private float m_Force;
		private Vector3 m_FirstVelocity;
		private Vector3 m_Velocity;
		private Vector3 m_FirstAngularVelocity;
		private Vector3 m_AngularVelocity;
		private float m_Inertia;
		private float m_BufferedDelta;
		private const float m_TimeStep = 0.001f;
		private const float g = 9.82f;
		private float m_Angle;
		private Vector3 m_FirstRelativeVelocity;
		private float m_Time;
		private float m_SlidingDuration;
		private float m_StaticFrictionCoefficient = 0.2f;
		private float m_RollingFrictionCoefficient = 0.015f;
		private Vector3 m_EndPos;
		private bool m_Sliding = false;
		private bool m_Rolling = false;
		private float m_FirstRollingDuration;
		public bool exact = false;

		void Start()
		{
			m_Radius = 0.056f;
			m_Inertia = (2.0f/5.0f) * m_Mass * m_Radius * m_Radius;
		}

		Vector3 Rotate2D(Vector3 v, float angle)
		{
			float[,] tMatrix = new float[2, 2]
			{ 	
				{ Mathf.Cos(m_ForwardAngle), - Mathf.Sin(m_ForwardAngle)},
				{ Mathf.Sin(m_ForwardAngle), Mathf.Cos(m_ForwardAngle) }
			};
			
			return new Vector2(tMatrix[0, 0] * v.x + tMatrix[0, 1] * v.z, 
			                   tMatrix[1, 0] * v.x + tMatrix[1, 1] * v.z);
		}

		Vector3 Rotate2D(Vector2 v, float angle)
		{
			float[,] tMatrix = new float[2, 2]
			{ 	
				{ Mathf.Cos(m_ForwardAngle), - Mathf.Sin(m_ForwardAngle)},
				{ Mathf.Sin(m_ForwardAngle), Mathf.Cos(m_ForwardAngle) }
			};
			
			return new Vector2(tMatrix[0, 0] * v.x + tMatrix[0, 1] * v.y, 
			                   tMatrix[1, 0] * v.x + tMatrix[1, 1] * v.y);
		}

		public void Strike(Main.CueInfo info)
		{
			float force;
			float a = info.position.x;
			float b = info.position.y;
			
			float aSqr = a * a;
			float bSqr = b * b;

			float c = Mathf.Abs(Mathf.Sqrt(Mathf.Pow(m_Radius, 2) - aSqr - bSqr));
			float cSqr = c * c;

			m_ForwardAngle = info.forwardAngle;
			float strikeAngle = Mathf.Atan2(a, m_Radius);
			//m_ForwardAngle += strikeAngle;
			
			force = 2 * info.mass * info.cueVelocity;

			force = force / (1 + (m_Mass / info.mass) + (5 / 2 * m_Radius)
				* (aSqr
				   + (bSqr * Mathf.Pow(Mathf.Cos(info.angle), 2)) 
				   + (cSqr * Mathf.Pow(Mathf.Sin(info.angle), 2))
				   - (2 * b * c * Mathf.Cos(info.angle) * Mathf.Sin(info.angle))
				   ));
			
			float ax = -c * force * Mathf.Sin(info.angle) + b * force * Mathf.Cos(info.angle);
			float ay =  a * force * Mathf.Sin(info.angle);
			float az = -a * force * Mathf.Cos(info.angle);

			m_AngularVelocity = (1.0f / m_Inertia) * (new Vector3(ax, ay, az));
			m_FirstAngularVelocity = m_AngularVelocity;

			m_Force = force;
			m_Velocity = new Vector3(0, (-m_Force / m_Mass) * Mathf.Cos(info.angle), /*0.0f Assumed to be zero.*/ (-m_Force / m_Mass) * Mathf.Sin(info.angle));
			m_FirstVelocity = m_Velocity;
			m_FirstRelativeVelocity = m_Velocity + Vector3.Cross(m_Radius * Vector3.up, m_AngularVelocity);

			Debug.Log("Force: " + force);
			Debug.Log(string.Format("Velocity: {0}", m_Velocity));
			Debug.Log(string.Format("Angular Velocity: {0}", m_AngularVelocity));
			m_SlidingDuration = (2.0f * m_FirstRelativeVelocity.magnitude) / (7.0f * m_StaticFrictionCoefficient * g);
			m_Angle = info.angle;

			m_EndPos.x = m_Velocity.magnitude * m_SlidingDuration 
				- ((1.0f / 2.0f) * m_StaticFrictionCoefficient * g * m_SlidingDuration * m_SlidingDuration * m_FirstRelativeVelocity.normalized.x);

			m_EndPos.z = 
				+ ((1.0f / 2.0f) * m_StaticFrictionCoefficient * g * m_SlidingDuration * m_SlidingDuration * m_FirstRelativeVelocity.normalized.y);

			m_EndPos = transform.position - Rotate2D(m_EndPos, m_ForwardAngle);

			m_Sliding = true;
			m_Rolling = false;

			Still = false;

			m_Time = 0.0f;
		}

		void Update()
		{
			m_BufferedDelta += Time.deltaTime * 1f;
			if (Still)
			{
				m_BufferedDelta = 0.0f;
			}
			else
			{
				for (;m_BufferedDelta > m_TimeStep; m_BufferedDelta -= m_TimeStep)
				{
					m_Time += m_TimeStep;
					Integrate();
				}
			}
		}

		void Integrate()
		{
			Vector3 expectedPosition = IntegrateExactPosition (transform.position, m_Time, m_TimeStep);
			if (exact) 
			{
				transform.position = expectedPosition;
				return;
			}

			Vector3 vAngRel = Vector3.Cross(m_Radius * Vector3.up, m_AngularVelocity);

			float sliding = (2.0f * vAngRel.magnitude) / (7.0f * m_StaticFrictionCoefficient * g);
			Vector3 relative = m_Velocity + Vector3.Cross (m_Radius * Vector3.up, m_AngularVelocity);

			if (m_Time < m_SlidingDuration) 
			{
				m_Velocity -= m_Velocity.normalized * m_StaticFrictionCoefficient * g * m_TimeStep;
				//m_AngularVelocity -= m_AngularVelocity.normalized * (((5.0f * m_StaticFrictionCoefficient * g) / (2.0f * m_Radius)) * m_TimeStep);

				m_AngularVelocity -= m_AngularVelocity.normalized * m_TimeStep * g * m_StaticFrictionCoefficient * m_Radius;

				m_Velocity -= vAngRel * m_TimeStep * m_Radius * g * m_StaticFrictionCoefficient;
			}
			else
			{
				m_Velocity -= m_Velocity.normalized * m_RollingFrictionCoefficient * g * m_TimeStep;
				m_AngularVelocity = m_AngularVelocity.normalized * (m_Velocity.magnitude / m_Radius);
			}

			Vector2 pos;

			pos.x = -m_Velocity.y * m_TimeStep;
			pos.y = m_Velocity.x * m_TimeStep;
			pos = Rotate2D (pos, m_ForwardAngle);

			transform.position = transform.position - new Vector3(pos.x, 0, pos.y);

			Debug.DrawLine (transform.position, expectedPosition);
		}

		Vector3 IntegrateExactPosition(Vector3 currentPosition, float currentTime, float deltaTime)
		{
			Vector3 vel = CalculateVelocity(currentTime);
			Vector2 pos;

			pos.x = -vel.y * deltaTime;
			pos.y = vel.x * deltaTime;
			pos = Rotate2D (pos, m_ForwardAngle);

			return currentPosition - new Vector3(pos.x, 0, pos.y);
		}

		private Vector3 CalculateVelocity(float time)
		{
			float sfc = m_StaticFrictionCoefficient;
			float rfc = m_RollingFrictionCoefficient;

			if (time < m_SlidingDuration) 
			{
				return m_FirstVelocity - sfc * g * time * CalculateRelativeVelocity(0).normalized;
			} 
			else 
			{
				Vector3 v = m_FirstVelocity - sfc * g * m_SlidingDuration * CalculateRelativeVelocity(0).normalized;
				return v - rfc * g * time * v.normalized;
			}
		}

		private Vector3 CalculateRelativeVelocity(float time)
		{
			float sfc = m_StaticFrictionCoefficient;
			float rfc = m_RollingFrictionCoefficient;

			return m_FirstRelativeVelocity - (7.0f / 2.0f) * sfc * g * time * m_FirstRelativeVelocity.normalized;
		}

		private Vector3 CalculateAngularVelocity(float time)
		{
			float sfc = m_StaticFrictionCoefficient;
			float rfc = m_RollingFrictionCoefficient;

			if (time < m_SlidingDuration) {
				return m_FirstAngularVelocity 
					- (((5.0f * sfc * g) / (2.0f * m_Radius)) 
					   * time) * Vector3.Cross (Vector3.up, CalculateRelativeVelocity(0).normalized);
			} 
			else 
			{
				Vector3 rVel = CalculateRelativeVelocity(m_SlidingDuration);

				Vector3 angular = m_FirstAngularVelocity 
					- (((5.0f * sfc * g) / (2.0f * m_Radius)) 
					   * m_SlidingDuration) * Vector3.Cross(Vector3.up, rVel.normalized);

				Vector3 vel = CalculateVelocity(time);

				return angular.normalized * (vel.magnitude / m_Radius);
			}
		}

		public void Collide(Vector3 otherPosition, Vector3 collisionNormal, Vector3 otherAngularVelocity, Vector3 otherVelocity)
		{
			Debug.Log("Collide");

		}

		void OnGUI()
		{

		}
	}
}

