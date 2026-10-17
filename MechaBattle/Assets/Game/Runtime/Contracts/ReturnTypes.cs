namespace MechaBattle.Contracts
{
    public readonly struct EmergencyReturnSnapshot
    {
        public readonly bool Channeling,InReserve; public readonly float SecondsRemaining,Progress;
        public EmergencyReturnSnapshot(bool channeling,bool reserve,float remaining,float progress)
        {Channeling=channeling;InReserve=reserve;SecondsRemaining=remaining;Progress=progress;}
    }
}
