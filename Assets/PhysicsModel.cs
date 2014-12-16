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
		public bool old = false;
		public bool precise = false;

		public float m_ForwardAngle;
		private float m_Mass = 0.15f;
		private float m_Radius;
		private float m_Force;
		private Vector3 m_Velocity;
		private Vector3 m_AngularVelocity;
		private float m_Inertia;
		private float m_BufferedDelta;
		private const float m_TimeStep = 0.01f;
		private const float g = 9.82f;
		private float m_Angle;
		private Vector3 m_RelativeVelocity;
		private float m_Time;
		private float m_FirstDuration;
		private float m_StaticFrictionCoefficient = 0.2f;
		private float m_RollingFrictionCoefficient = 0.015f;
		private Vector3 m_EndPos;
		private bool m_Sliding = false;
		private bool m_Rolling = false;
		private float m_FirstRollingDuration;

		private Vector3 m_FirstVelocity;
		private Vector3 m_FirstRelativeVelocity;
		private Vector3 m_FirstAngularVelocity;

		void Start()
		{
			Still = true;
			m_Radius = 0.056f;
			m_Inertia = (2.0f/5.0f) * m_Mass * m_Radius * m_Radius;
		}

		Vector3 Rotate2D(Vector3 v, float angle)
		{
			float[,] tMatrix = new float[2, 2]
			{ 	
				{ Mathf.Cos(angle), - Mathf.Sin(angle)},
				{ Mathf.Sin(angle), Mathf.Cos(angle) }
			};
			
			return new Vector3(tMatrix[0, 0] * v.x + tMatrix[0, 1] * v.z,
			                   0,  
			                   tMatrix[1, 0] * v.x + tMatrix[1, 1] * v.z);
		}

		Vector3 Rotate2D(Vector2 v, float angle)
		{
			float[,] tMatrix = new float[2, 2]
			{ 	
				{ Mathf.Cos(angle), - Mathf.Sin(angle)},
				{ Mathf.Sin(angle), Mathf.Cos(angle) }
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

			m_Force = force;
			m_Velocity = new Vector3(0, (-m_Force / m_Mass) * Mathf.Cos(info.angle), /*0.0f Assumed to be zero.*/ (-m_Force / m_Mass) * Mathf.Sin(info.angle));
			m_RelativeVelocity = m_Velocity + Vector3.Cross(m_Radius * Vector3.up, m_AngularVelocity);

			Debug.Log("Force: " + force);
			Debug.Log(string.Format("Velocity: {0}", m_Velocity));
			Debug.Log(string.Format("Angular Velocity: {0}", m_AngularVelocity));
			m_FirstDuration = (2.0f * m_RelativeVelocity.magnitude) / (7.0f * m_StaticFrictionCoefficient * g);
			m_Angle = info.angle;

			m_EndPos.x = m_Velocity.magnitude * m_FirstDuration 
				- ((1.0f / 2.0f) * m_StaticFrictionCoefficient * g * m_FirstDuration * m_FirstDuration * m_RelativeVelocity.normalized.x);

			m_EndPos.z = 
				+ ((1.0f / 2.0f) * m_StaticFrictionCoefficient * g * m_FirstDuration * m_FirstDuration * m_RelativeVelocity.normalized.y);

			m_EndPos = transform.position - Rotate2D(m_EndPos, m_ForwardAngle);

			m_Sliding = true;
			m_Rolling = false;

			Still = false;

			m_FirstVelocity = m_Velocity;
			m_FirstRelativeVelocity = m_RelativeVelocity;
			m_FirstAngularVelocity = m_AngularVelocity;
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
			if (old && precise)
			{
				//IntegrateOLD();
			}
			else if (precise)
			{
				transform.position = AdvancePrecisePosition(transform.position, m_Time, m_TimeStep);
			}
			else
			{
				float sfc = m_StaticFrictionCoefficient;
				float rfc = m_RollingFrictionCoefficient;

				//m_RelativeVelocity -= (7.0f / 2.0f) * sfc * g * m_TimeStep * m_RelativeVelocity.normalized;
				m_AngularVelocity -= (((5.0f * sfc * g) / (2.0f * m_Radius)) * m_TimeStep) * Vector3.Cross(Vector3.up, m_RelativeVelocity.normalized);
				m_Velocity -= sfc * g * m_TimeStep * m_RelativeVelocity.normalized;

				Vector2 pos = Vector2.zero;
				pos.x = -m_Velocity.y * m_TimeStep;
				pos.y = m_Velocity.x * m_TimeStep;
				pos = Rotate2D(pos, m_ForwardAngle);
				transform.position = transform.position - new Vector3(pos.x, 0, pos.y);
			}
		}

		void IntegrateOLD()
		{
			float[,] tMatrix = new float[2, 2]
			{ 	
				{ Mathf.Cos(m_ForwardAngle), - Mathf.Sin(m_ForwardAngle)},
				{ Mathf.Sin(m_ForwardAngle), Mathf.Cos(m_ForwardAngle) }
			};

			float sfc = m_StaticFrictionCoefficient;
			float rfc = m_RollingFrictionCoefficient;

			/* Relative velocity */
			Vector3 rVel = m_RelativeVelocity;

			Vector2 pos = Vector3.zero;

			/* I THINK!
			 * This is assumed to be correct. This is not the integrated version of the 
			 * formula. However, it appears to work. rVel is the relative velocity rv at time 0 (rv(0)),
			 * and m_RelativeVelocity is the relative velocity at time t (rv(t)). Now, I assume that rv(1)
			 * is related to rv(2) the same way that rv(0) is related to rv(1). Given that assumption, and
			 * this I take to be self evident given how the formula looks, I can use that to integrate the
			 * relative velocity using the euler method.
			 */
			//m_RelativeVelocity = rVel - (7.0f / 2.0f) * sfc * g * m_TimeStep * rVel.normalized;
			rVel = m_RelativeVelocity - (7.0f / 2.0f) * sfc * g * m_Time * m_RelativeVelocity.normalized;

			float slidingDuration = (2.0f * rVel.magnitude) / (7.0f * sfc * g);

			bool forceRoll = false;
			if (rVel.magnitude <= ((7.0f / 2.0f) * sfc * g * m_TimeStep * rVel.normalized).magnitude || 
			    slidingDuration - m_TimeStep < float.Epsilon)
			{
				forceRoll = true;
			}

			bool rolling = rVel.magnitude <= Mathf.Epsilon || forceRoll || m_Rolling;
			bool sliding = !rolling;
		
			bool firstRoll = sliding != m_Sliding;
			m_Sliding = sliding;
			m_Rolling = rolling;

			if (sliding)
			{
				/* I THINK!	*/

				/* Surely this works. Given the formula in the patper: 
				 * Acceleration is sfc*g*rVel.normalized (the derivative of veloicty), and velocity = a*t.
				 */
				Vector3 vel = m_Velocity - sfc * g * m_Time * m_RelativeVelocity.normalized;
				//m_Velocity = m_Velocity - sfc * g * m_TimeStep * rVel.normalized; m_Velocity - sfc * g * m_TimeStep * rVel.normalized;

				/* hm */
				Vector3 angular = m_AngularVelocity 
					- (((5.0f * sfc * g) / (2.0f * m_Radius)) 
					   * m_Time) * Vector3.Cross(Vector3.up, m_RelativeVelocity.normalized);

				/*m_AngularVelocity = m_AngularVelocity 
					- (((5.0f * sfc * g) / (2.0f * m_Radius)) 
						* m_TimeStep) * Vector3.Cross(Vector3.up, rVel.normalized);*/
				
				Debug.Log("ANGDIFF: " + (angular - CalculateAngularVelocity(m_Time)));

				angular = Rotate2D(angular, m_ForwardAngle);

				transform.Rotate(-angular.z * Mathf.Rad2Deg * m_TimeStep, -angular.y * Mathf.Rad2Deg * m_TimeStep, -angular.x * Mathf.Rad2Deg * m_TimeStep);

				pos.x = -vel.y * m_TimeStep;
				pos.y = vel.x * m_TimeStep;
				
				/* tMatrix * pos */
	
				pos = Rotate2D(pos, m_ForwardAngle);
				Vector3 next = transform.position - new Vector3(pos.x, 0, pos.y) * ((1 / m_TimeStep) * m_Velocity.magnitude);

				Debug.DrawLine(transform.position, next, Color.blue);

				transform.position = transform.position - new Vector3(pos.x, 0, pos.y);
			}
			else
			{
				float rollingDuration = m_Velocity.magnitude / (rfc * g);

				if (firstRoll)
				{
					Debug.Log("Calculated Slide Duration: " + m_FirstDuration + ", Actual Duration: " + m_Time + ", Difference: " + (m_FirstDuration - m_Time));
					Debug.Log("Rolling duration: " + rollingDuration);
					m_Velocity = m_Velocity - sfc * g * m_Time * m_RelativeVelocity.normalized;
					m_Time = m_TimeStep;
					m_FirstRollingDuration = m_Velocity.magnitude / (rfc * g);
					m_AngularVelocity = m_AngularVelocity 
						- (((5.0f * sfc * g) / (2.0f * m_Radius)) 
						   * m_Time) * Vector3.Cross(Vector3.up, rVel.normalized);
					Debug.Log("First roll");
				}
				Vector3 vel = m_Velocity - rfc * g * m_Time * m_Velocity.normalized;

				pos.x = -vel.y * m_TimeStep;
				pos.y = vel.x * m_TimeStep;

				Vector3 angularNormal = m_AngularVelocity.normalized;
				Vector3 angular = angularNormal * (vel.magnitude / m_Radius);

				Debug.Log("ANGDIFF: " + (angular - CalculateAngularVelocity(m_Time)));

				Vector2 tPos = new Vector2(tMatrix[0, 0] * pos.x + tMatrix[0, 1] * pos.y, 
				                           tMatrix[1, 0] * pos.x + tMatrix[1, 1] * pos.y);

				transform.position = transform.position - new Vector3(tPos.x, 0, tPos.y);


				//This is haaard.
				transform.Rotate(-angular.x * Mathf.Rad2Deg * m_TimeStep, -angular.y * Mathf.Rad2Deg * m_TimeStep, -angular.z * Mathf.Rad2Deg * m_TimeStep);
				
				if (rollingDuration - m_TimeStep < 0)
				{
					Still = true;
					m_Time -= m_TimeStep;
					Debug.Log("Calculated Full Duration: " + (m_FirstRollingDuration) + ", Actual Duration: " + m_Time + ", Difference: " + (m_FirstRollingDuration - m_Time));
				}
			}

			Debug.DrawLine(transform.position, m_EndPos, Color.red);
		}

		private Vector3 AdvancePrecisePosition(Vector3 currentPos, float time, float deltaTime)
		{
			Vector3 pos = Vector3.zero;
			Vector3 vel = CalculateVelocity(time);
			pos.x = -vel.y * deltaTime;
			pos.z = vel.x * deltaTime;
			return currentPos - Rotate2D(pos, m_ForwardAngle);
		}

		private Vector3 CalculateAngularVelocity(float time)
		{
			float sfc = m_StaticFrictionCoefficient;

			if (CalculateIsSliding(time))
			{
				return m_FirstAngularVelocity 
					- (((5.0f * sfc * g) / (2.0f * m_Radius)) 
					   * time) * Vector3.Cross(Vector3.up, CalculateRelativeVelocity(0f).normalized);
			}
			else
			{
				float slide = CalculateSlidingDuration(0f);

				Vector3 angular = m_FirstAngularVelocity 
				- (((5.0f * sfc * g) / (2.0f * m_Radius)) 
					   * slide) * Vector3.Cross(Vector3.up, CalculateRelativeVelocity(slide).normalized);

				Vector3 angularNormal = angular.normalized;
				return angularNormal * (CalculateVelocity(time).magnitude / m_Radius);
			}
		}

		private Vector3 CalculateRelativeVelocity(float time)
		{
			float sfc = m_StaticFrictionCoefficient;
			float rfc = m_RollingFrictionCoefficient;
			
			return m_FirstRelativeVelocity - (7.0f / 2.0f) * sfc * g * time * m_FirstRelativeVelocity.normalized;
		}

		private Vector3 CalculateVelocity(float time)
		{
			float sfc = m_StaticFrictionCoefficient;
			float rfc = m_RollingFrictionCoefficient;

			if (CalculateIsSliding(time))
			{
				return m_Velocity - sfc * g * time * CalculateRelativeVelocity(0f).normalized;
			}
			else
			{
				float sliding = CalculateSlidingDuration(0f);
				Vector3 vel = m_FirstVelocity - sfc * g * sliding * CalculateRelativeVelocity(0f).normalized;
				return vel - rfc * g * (sliding - time) * m_Velocity.normalized;;
			}

		}


		private float CalculateSlidingDuration(float time)
		{
			float sfc = m_StaticFrictionCoefficient;

			return (2.0f * CalculateRelativeVelocity(time).magnitude) / (7.0f * sfc * g);
		}

		private bool CalculateIsSliding(float time)
		{
			float slidingDuration = CalculateSlidingDuration(0f);
			return time < slidingDuration;
		}

		private bool CalculateIsRolling(float time)
		{
			return !CalculateIsSliding(time);
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

