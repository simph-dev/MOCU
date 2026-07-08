// Numbers are negative because 'enum' by default starts from 0 and goes on,
// thus custom SubStatuses without 'SubStatusImportance' may be processed incorrectly

public enum SubStatusImportance
{
    NonCritical = -2,
    Critical = -1
}