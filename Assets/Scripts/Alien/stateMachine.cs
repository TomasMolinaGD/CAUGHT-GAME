using UnityEngine;
//contiene idle y Partol

// TEMPORAL: FSM incompleta desactivada para permitir compilar y abrir Unity.
/*
public enum PoliceState
{
   idle,
   Patrol
}
public  class StateMachine 
{

   
   public State CurrentState {get; private set;}
   private Dictionary<Enum,State> states=new Dictionary<Enum,State>();
   public void RegisterState(Enum key, State state)
   {
      states[key] = state;
   }
   public void ChangeState(Enum key)
   {
      State newState = states[key];
      if(newState==CurrentState)
         return;
      CurrentState?.Exit();
      CurrentState = NewState;
      CurrentState.Enter();
   }
   public void Update()
   {
      CurrentState.Update();
   }

}
*/
