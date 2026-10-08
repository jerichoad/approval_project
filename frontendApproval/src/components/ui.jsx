import { forwardRef, useEffect, useRef } from "react";
import { cn } from "../lib/utils";

const variantCls = {
  primary: "bg-primary text-primary-foreground hover:bg-navy-700",
  secondary: "bg-navy-50 text-navy-700 hover:bg-navy-100",
  outline: "border bg-card text-foreground hover:bg-muted",
  ghost: "text-foreground hover:bg-muted",
  danger: "bg-error text-white hover:bg-error/90",
  success: "bg-success text-white hover:bg-success/90",
};

const sizeCls = {
  sm: "h-8 px-3 text-xs",
  md: "h-9 px-4 text-sm",
};

function buttonClass(variant = "primary", size = "md", className) {
  return cn(
    "inline-flex items-center justify-center gap-1.5 rounded-md font-semibold whitespace-nowrap transition-colors",
    "focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring",
    "disabled:pointer-events-none disabled:opacity-50",
    variantCls[variant],
    sizeCls[size],
    className,
  );
}

export const Button = forwardRef(function Button(
  { variant = "primary", size = "md", className, type = "button", ...props },
  ref,
) {
  return <button ref={ref} type={type} className={buttonClass(variant, size, className)} {...props} />;
});

const fieldCls =
  "h-9 w-full rounded-md border border-input bg-card px-3 text-sm text-foreground shadow-xs placeholder:text-muted-foreground focus-visible:border-ring focus-visible:outline-2 focus-visible:outline-ring/30 disabled:opacity-60 aria-invalid:border-error";

export const Input = forwardRef(function Input({ className, ...props }, ref) {
  return <input ref={ref} className={cn(fieldCls, className)} {...props} />;
});

export const Select = forwardRef(function Select({ className, children, ...props }, ref) {
  return (
    <select ref={ref} className={cn(fieldCls, "pr-8", className)} {...props}>
      {children}
    </select>
  );
});

export const Textarea = forwardRef(function Textarea({ className, ...props }, ref) {
  return <textarea ref={ref} className={cn(fieldCls, "h-auto min-h-24 py-2", className)} {...props} />;
});

export function Field({ label, htmlFor, error, hint, children }) {
  return (
    <div className="flex flex-col gap-1.5">
      <label htmlFor={htmlFor} className="text-xs font-semibold text-foreground">
        {label}
      </label>
      {children}
      {error ? (
        <p id={`${htmlFor}-error`} className="text-xs text-error">
          {error}
        </p>
      ) : hint ? (
        <p className="text-xs text-muted-foreground">{hint}</p>
      ) : null}
    </div>
  );
}

export function SegmentedControl({ value, onChange, options, label, invalid }) {
  return (
    <div
      role="radiogroup"
      aria-label={label}
      aria-invalid={invalid || undefined}
      className="inline-flex w-fit rounded-md border bg-muted/60 p-0.5 aria-invalid:border-error"
    >
      {options.map((o) => {
        const active = o.value === value;
        return (
          <button
            key={o.value}
            type="button"
            role="radio"
            aria-checked={active}
            onClick={() => onChange(o.value)}
            className={cn(
              "h-7 rounded px-3 text-xs font-semibold transition-colors focus-visible:outline-2 focus-visible:outline-ring",
              active ? "bg-card text-foreground shadow-sm" : "text-muted-foreground hover:text-foreground",
            )}
          >
            {o.label}
          </button>
        );
      })}
    </div>
  );
}

export function Dialog({ open, onClose, title, description, children, footer }) {
  const ref = useRef(null);

  useEffect(() => {
    const el = ref.current;
    if (!el) return;
    if (open && !el.open) el.showModal();
    if (!open && el.open) el.close();
  }, [open]);

  return (
    <dialog
      ref={ref}
      onClose={onClose}
      onCancel={(e) => {
        e.preventDefault();
        onClose();
      }}
      className="m-auto w-[min(32rem,calc(100vw-2rem))] rounded-lg border bg-card p-0 text-foreground shadow-xl backdrop:bg-navy-900/50"
    >
      {open ? (
        <div className="flex flex-col">
          <header className="border-b px-5 py-4">
            <h2 className="text-base font-semibold">{title}</h2>
            {description ? <p className="mt-0.5 text-xs text-muted-foreground">{description}</p> : null}
          </header>
          {children ? <div className="px-5 py-4">{children}</div> : null}
          {footer ? <footer className="flex justify-end gap-2 border-t bg-muted/40 px-5 py-3">{footer}</footer> : null}
        </div>
      ) : null}
    </dialog>
  );
}
