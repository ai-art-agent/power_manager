using LibreHardwareMonitor.Hardware;
using System.Collections.Generic;

namespace SensorTest;

/// <summary>
/// Обходит всё железо, обновляет значения датчиков.
/// </summary>
public class UpdateVisitor : IVisitor
{
    public void VisitComputer(IComputer computer) => computer.Traverse(this);

    public void VisitHardware(IHardware hardware)
    {
        hardware.Update();
        foreach (IHardware sub in hardware.SubHardware)
            sub.Accept(this);
    }

    public void VisitSensor(ISensor sensor) { }

    public void VisitParameter(IParameter parameter) { }
}
