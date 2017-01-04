using UnityEngine;
using System.Collections;
using System.Text;
using UnityEngine.UI;
// Needed //////////////////////////////////////////////////
using HoloLensXboxController;
///////////////////////////////////////////////////////////

public class ControllerInputExample : MonoBehaviour {
    
    // Needed //////////////////////////////////////////////////
    private ControllerInput controllerInput;
    ///////////////////////////////////////////////////////////

    // Use this for initialization
    void Start () {
        // Needed //////////////////////////////////////////////////
        controllerInput = new ControllerInput(0, 0.19f);
        // First parameter is the number, starting at zero, of the controller you want to follow.
        // Second parameter is the default “dead” value; meaning all stick readings less than this value will be set to 0.0.
        ///////////////////////////////////////////////////////////
    }

    void Update () {
        // Needed //////////////////////////////////////////////////
        controllerInput.Update();
        ///////////////////////////////////////////////////////////

        translateRotateScale();
        setAxisInputText();
        setButtonInputText();
        setJoyStickNamesText();
    }

    public float RotateAroundYSpeed = 2.0f;
    public float RotateAroundXSpeed = 2.0f;
    public float RotateAroundZSpeed = 2.0f;

    public float MoveHorizontalSpeed = 0.01f;
    public float MoveVerticalSpeed = 0.01f;

    public float ScaleSpeed = 1f;

    public Text AxisInputText;
    public Text ButtonInputText;
    public Text JoyStickNamesText;

    private string lastButtonDown = string.Empty;
    private string lastButtonUp = string.Empty;

    private void translateRotateScale()
    {
        float moveHorizontal = MoveHorizontalSpeed * controllerInput.GetAxisLeftThumbstickX();
        float moveVertical = MoveVerticalSpeed * controllerInput.GetAxisLeftThumbstickY();
        this.transform.Translate(moveHorizontal, moveVertical, 0.0f);

        float rotateAroundY = RotateAroundYSpeed * controllerInput.GetAxisRightThumbstickX();
        float rotateAroundX = RotateAroundXSpeed * controllerInput.GetAxisRightThumbstickY();
        float rotateAroundZ = RotateAroundZSpeed * (controllerInput.GetAxisRightTrigger() - controllerInput.GetAxisLeftTrigger());
        this.transform.Rotate(rotateAroundX, -rotateAroundY, rotateAroundZ);

        if (controllerInput.GetButton(ControllerButton.DPadUp))
            this.transform.localScale = this.transform.localScale + (this.transform.localScale * ScaleSpeed * Time.deltaTime);

        if (controllerInput.GetButton(ControllerButton.DPadDown))
            this.transform.localScale = this.transform.localScale - (this.transform.localScale * ScaleSpeed * Time.deltaTime);
    }

    private void setAxisInputText()
    {
        AxisInputText.text =
            "LeftThumbstickX: " + controllerInput.GetAxisLeftThumbstickX().ToString() + System.Environment.NewLine +
            "LeftThumbstickY: " + controllerInput.GetAxisLeftThumbstickY().ToString() + System.Environment.NewLine +
            "RightThumbstickX: " + controllerInput.GetAxisRightThumbstickX().ToString() + System.Environment.NewLine +
            "RightThumbstickY: " + controllerInput.GetAxisRightThumbstickY().ToString() + System.Environment.NewLine +
            "LeftTrigger: " + controllerInput.GetAxisLeftTrigger().ToString() + System.Environment.NewLine +
            "RightTrigger: " + controllerInput.GetAxisRightTrigger().ToString();
    }  
    
    private void setButtonInputText()
    {
        setLastButtonDown();
        setLastButtonUp();

        ButtonInputText.text =
            "Last Button Down: " + lastButtonDown + System.Environment.NewLine +
            "Last Button Up: " + lastButtonUp + System.Environment.NewLine +
            "A: " + ((controllerInput.GetButton(ControllerButton.A)) ? "YES" : "NO") + System.Environment.NewLine +
            "B: " + ((controllerInput.GetButton(ControllerButton.B)) ? "YES" : "NO") + System.Environment.NewLine +
            "X: " + ((controllerInput.GetButton(ControllerButton.X)) ? "YES" : "NO") + System.Environment.NewLine +
            "Y: " + ((controllerInput.GetButton(ControllerButton.Y)) ? "YES" : "NO") + System.Environment.NewLine +
            "LB: " + ((controllerInput.GetButton(ControllerButton.LeftShoulder)) ? "YES" : "NO") + System.Environment.NewLine +
            "RB: " + ((controllerInput.GetButton(ControllerButton.RightShoulder)) ? "YES" : "NO") + System.Environment.NewLine +
            "SHOW ADDRESS BAR: " + ((controllerInput.GetButton(ControllerButton.View)) ? "YES" : "NO") + System.Environment.NewLine +
            "SHOW MENU: " + ((controllerInput.GetButton(ControllerButton.Menu)) ? "YES" : "NO") + System.Environment.NewLine +
            "LEFT STICK CLICK: " + ((controllerInput.GetButton(ControllerButton.LeftThumbstick)) ? "YES" : "NO") + System.Environment.NewLine +
            "RIGHT STICK CLICK: " + ((controllerInput.GetButton(ControllerButton.RightThumbstick)) ? "YES" : "NO") + System.Environment.NewLine +
            "DPadDown: " + ((controllerInput.GetButton(ControllerButton.DPadDown)) ? "YES" : "NO") + System.Environment.NewLine +
            "DPadUp: " + ((controllerInput.GetButton(ControllerButton.DPadUp)) ? "YES" : "NO") + System.Environment.NewLine +
            "DPadLeft: " + ((controllerInput.GetButton(ControllerButton.DPadLeft)) ? "YES" : "NO") + System.Environment.NewLine +
            "DPadRight: " + ((controllerInput.GetButton(ControllerButton.DPadRight)) ? "YES" : "NO");
    }

    private void setLastButtonDown()
    {
        if (controllerInput.GetButtonDown(ControllerButton.A))
            lastButtonDown = "A";

        if (controllerInput.GetButtonDown(ControllerButton.B))
            lastButtonDown = "B";

        if (controllerInput.GetButtonDown(ControllerButton.X))
            lastButtonDown = "X";

        if (controllerInput.GetButtonDown(ControllerButton.Y))
            lastButtonDown = "Y";

        if (controllerInput.GetButtonDown(ControllerButton.LeftShoulder))
            lastButtonDown = "LB";

        if (controllerInput.GetButtonDown(ControllerButton.RightShoulder))
            lastButtonDown = "RB";

        if (controllerInput.GetButtonDown(ControllerButton.View))
            lastButtonDown = "SHOW ADDRESS";

        if (controllerInput.GetButtonDown(ControllerButton.Menu))
            lastButtonDown = "SHOW MENU";

        if (controllerInput.GetButtonDown(ControllerButton.LeftThumbstick))
            lastButtonDown = "LEFT STICK CLICK";

        if (controllerInput.GetButtonDown(ControllerButton.RightThumbstick))
            lastButtonDown = "RIGHT STICK CLICK";

        if (controllerInput.GetButtonDown(ControllerButton.DPadDown))
            lastButtonDown = "DPadDown";

        if (controllerInput.GetButtonDown(ControllerButton.DPadUp))
            lastButtonDown = "DPadUp";

        if (controllerInput.GetButtonDown(ControllerButton.DPadLeft))
            lastButtonDown = "DPadLeft";

        if (controllerInput.GetButtonDown(ControllerButton.DPadRight))
            lastButtonDown = "DPadRight";
    }

    private void setLastButtonUp()
    {
        if (controllerInput.GetButtonUp(ControllerButton.A))
            lastButtonUp = "A";

        if (controllerInput.GetButtonUp(ControllerButton.B))
            lastButtonUp = "B";

        if (controllerInput.GetButtonUp(ControllerButton.X))
            lastButtonUp = "X";

        if (controllerInput.GetButtonUp(ControllerButton.Y))
            lastButtonUp = "Y";

        if (controllerInput.GetButtonUp(ControllerButton.LeftShoulder))
            lastButtonUp = "LB";

        if (controllerInput.GetButtonUp(ControllerButton.RightShoulder))
            lastButtonUp = "RB";

        if (controllerInput.GetButtonUp(ControllerButton.View))
            lastButtonUp = "SHOW ADDRESS";

        if (controllerInput.GetButtonUp(ControllerButton.Menu))
            lastButtonUp = "SHOW MENU";

        if (controllerInput.GetButtonUp(ControllerButton.LeftThumbstick))
            lastButtonUp = "LEFT STICK CLICK";

        if (controllerInput.GetButtonUp(ControllerButton.RightThumbstick))
            lastButtonUp = "RIGHT STICK CLICK";

        if (controllerInput.GetButtonUp(ControllerButton.DPadDown))
            lastButtonUp = "DPadDown";

        if (controllerInput.GetButtonUp(ControllerButton.DPadUp))
            lastButtonUp = "DPadUp";

        if (controllerInput.GetButtonUp(ControllerButton.DPadLeft))
            lastButtonUp = "DPadLeft";

        if (controllerInput.GetButtonUp(ControllerButton.DPadRight))
            lastButtonUp = "DPadRight";
    }

    private void setJoyStickNamesText()
    {
        string[] joystickNames = Input.GetJoystickNames();

        StringBuilder sb = new StringBuilder();
        sb.Append("JoySticks: ");
        sb.AppendLine(joystickNames.Length.ToString());
        foreach (string joystickName in joystickNames)
        {
            sb.AppendLine(joystickName);
        }

        JoyStickNamesText.text = sb.ToString();
    }

}
