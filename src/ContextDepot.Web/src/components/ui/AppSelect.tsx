import {
  Select,
  SelectContent,
  SelectGroup,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "./select";

interface Option {
  value: string;
  label: string;
}
interface Props {
  label: string;
  value: string;
  options: readonly Option[];
  onChange: (value: string) => void;
  disabled?: boolean;
  className?: string;
  id?: string;
}

export function AppSelect({
  label,
  value,
  options,
  onChange,
  disabled,
  className,
  id,
}: Props) {
  return (
    <Select value={value} onValueChange={onChange} disabled={disabled}>
      <SelectTrigger id={id} aria-label={label} className={className}>
        <SelectValue />
      </SelectTrigger>
      <SelectContent>
        <SelectGroup>
          {options.map((option) => (
            <SelectItem key={option.value} value={option.value}>
              {option.label}
            </SelectItem>
          ))}
        </SelectGroup>
      </SelectContent>
    </Select>
  );
}
