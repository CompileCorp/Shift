namespace Compile.Shift.Model;

public enum MigrationAction
{
    CreateTable = 0,
    AddColumn = 1,
    AddForeignKey = 2,
    AlterColumn = 3,
    AddIndex = 4,
}